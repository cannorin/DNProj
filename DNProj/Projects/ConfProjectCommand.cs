/*
DNProj - Manage your *proj and sln with commandline.
Copyright (c) 2016 cannorin

This file is part of DNProj.

DNProj is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using NX;
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class ConfProjectCommand : Command
    {
        string projName;
        Option<int> gIndex;

        public ConfProjectCommand()
            : base("dnproj conf", 
                   @"show and edit project configurations.", "show and edit project configurations.", "<command>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
            Options.Add("i=|group-index=", "specify index of property group you want to show or edit. indices are shown as \n'PropertyGroup #<index>'. [default=0]", i => gIndex = Option.Some(int.Parse(i)));

            @"$ dnproj conf show
$ dnproj conf set -i 1 OutputPath bin/Debug
$ dnproj conf set-condition Platform "" '\$(Platform)' == '' ""
$ dnproj conf rm OutputPath -i 1
$ dnproj conf add-group
$ dnprij conf set-group-condition -i 2 "" \$(MyCondition) == 'true' ""
$ dnproj conf rm-group -i 2"
                .Split('\n').Iter(this.AddExample);

            this.AddWarning("you must escape the dollar sign '$' inside \"\" as \"\\$\", or use '' instead.");

            Commands["show"] = Child(
                args =>
                {
                    var p = Commands["show"].LoadProject(ref args, ref projName);
                    var gs = Groups(p);
                    if (args.LengthNX() == 0 && !gIndex.HasValue)
                        foreach (var pg in gs.MapI((x, i) => new {v = x, i = i}))
                            printPropertyGroup(pg.v, pg.i);
                    else if (args.LengthNX() == 0)
                    {
                        var g = gs.Nth(gIndex.Value).AbortNone(() =>
                            Report.Fatal("index out of range.")
                        );
                        printPropertyGroup(g, gIndex.Value);
                    } 
                }, "dnproj conf show", "show project configurations.", "show project configurations.", "[options]");

            Commands["set"] = Child(
                args =>
                {
                    var p = Commands["set"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    );
                    if (!args.Any())
                        Report.Fatal("missing parameter.");
                    else
                    {
                        var name = args.First();
                        var val = args.LengthNX() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        if (g.Cast<BuildProperty>().Any(x => x.Name == name))
                            g.SetProperty(name, val);
                        else
                            g.AddNewProperty(name, val);
                    }
                    printPropertyGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj conf set", "add or change property value.\ngiving empty <value> will set empty value.", "add or change property value.", "<name>", "<value>", "[options]");

            Commands["set-condition"] = Child(
                args =>
                {
                    var p = Commands["set-condition"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    ); 
                    if (!args.Any())
                        Report.Fatal("missing parameter.");
                    else
                    {
                        var name = args.First();
                        var val = args.LengthNX() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        g.Cast<BuildProperty>()
                                .Find(x => x.Name == name)
                                .Match(
                            y => y.Condition = val,
                            () => Report.Fatal("property with name '{0}' doesn't exist.", name)
                        );
                        printPropertyGroup(g, Groups(p).IndexOf(g));
                    }
                    p.Save(p.FullFileName);
                }, "dnproj conf set-condition", "set conditon to property.\ngiving empty <condition> will remove condition.", "set condition to property.", "<name>", "<condition>", "[options]");

            Commands["rm"] = Child(
                args =>
                {
                    var p = Commands["rm"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    ); 
                    foreach (var s in args)
                    {
                        if (g.Cast<BuildProperty>().Any(x => x.Name == s))
                            g.RemoveProperty(s);
                        else
                            Report.Fatal("property with name '{0}' doesn't exist.", s);
                    }
                    printPropertyGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj conf rm", "remove property.", "remove property.", "<name>+", "[options]");

            Commands["add-group"] = Child(
                args =>
                {
                    var p = Commands["add-group"].LoadProject(ref args, ref projName);
                    var cond = args.JoinToString(" ");
                    var g = p.AddNewPropertyGroup(false);
                    if (!string.IsNullOrEmpty(cond))
                        g.Condition = cond;
                    p.Save(p.FullFileName);
                }, "dnproj conf add-group", "add property group.", "add property group.", "[condition]", "[options]");

            Commands["set-group-condition"] = Child(
                args =>
                {
                    var p = Commands["set-group-condition"].LoadProject(ref args, ref projName);
                    if (!args.Any())
                        Report.Fatal("missing parameter.");

                    var cond = args.JoinToString(" ");
                    gIndex.Map(Groups(p).Nth)
                    .Flatten()
                    .Match(
                        g =>
                        {
                            if (!string.IsNullOrEmpty(cond))
                                g.Condition = cond;
                            p.Save(p.FullFileName); 
                        },
                        () => Report.Fatal("group index not specified.")
                    );
                }, "dnproj set-group-condition", "set condition to specified group. -i option is required. \ngiving empty condition will remove condition.", "set condition to group.", "[condition]", "[options]");

            Commands["rm-group"] = Child(
                args =>
                {
                    var p = Commands["rm-group"].LoadProject(ref args, ref projName);

                    gIndex.Map(Groups(p).Nth)
                    .Flatten()
                    .Match(
                        g =>
                        {
                            p.RemovePropertyGroup(g);
                            p.Save(p.FullFileName); 
                        },
                        () => Report.Fatal("group index not specified.")
                    );
                }, "dnproj rm-group", "remove specified group. -i option is required.", "set condition to group.", "[options]");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args)
        {
            return this.GenerateSuggestions(
                args,
                i =>
                {
                    switch (i)
                    {
                        case "-p":
                        case "--proj":
                            return CommandSuggestion.Files("*proj");
                        case "-i":
                        case "--group-index":
                            var p = ProjectTools.GetProject(projName);
                            if(p.HasValue)
                                return CommandSuggestion.Values(Seq.ZeroTo(Groups(p.Value).Count()).Map(x => x.ToString()));
                            else
                                return CommandSuggestion.None;
                        default:
                            return CommandSuggestion.None;
                    }
                }
            );
        }

        public override IEnumerable<CommandSuggestion> GetChildSuggestions(ChildCommand child, IEnumerable<string> args)
        {
            switch(child.Name.Split(' ').Last())
            {
                case "show":
                    yield return CommandSuggestion.None;
                    break; 
                case "set":
                    if(!args.Any())
                    {
                        var p = ProjectTools.GetProject(projName);
                        if (p.HasValue)
                        {
                            var gs = Groups(p.Value);
                            var idx = gIndex.Default(0);
                            yield return 
                                gs.Nth(idx)
                                  .Map(x => x.Cast<BuildProperty>().Map(y => y.Name))
                                  .Map(CommandSuggestion.Values)
                                  .Default(CommandSuggestion.None);
                        }
                        else
                            yield return CommandSuggestion.None;
                    }
                    else
                        yield return CommandSuggestion.None;
                    break;

                case "set-condition":
                    if(!args.Any())
                    {
                        var p = ProjectTools.GetProject(projName);
                        if (p.HasValue)
                        {
                            var gs = Groups(p.Value);
                            var idx = gIndex.Default(0);
                            yield return 
                                gs.Nth(idx)
                                    .Map(x => x.Cast<BuildProperty>().Map(y => y.Name))
                                    .Map(CommandSuggestion.Values)
                                    .Default(CommandSuggestion.None);
                        }
                        else
                            yield return CommandSuggestion.None;
                    }
                    else
                        yield return CommandSuggestion.None;
                    break;

                case "rm":
                    {
                        var p = ProjectTools.GetProject(projName);
                        if (p.HasValue)
                        {
                            var gs = Groups(p.Value);
                            var idx = gIndex.Default(0);
                            yield return 
                                gs.Nth(idx)
                                    .Map(x => x.Cast<BuildProperty>().Map(y => y.Name))
                                    .Map(CommandSuggestion.Values)
                                    .Default(CommandSuggestion.None);
                        }
                        else
                            yield return CommandSuggestion.None;
                    }
                    break;

                default:
                    yield return CommandSuggestion.None;
                    break;
            }
        }

        IEnumerable<BuildPropertyGroup> Groups(Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>().Filter(x => !x.IsImported);
        }

        void printPropertyGroup(BuildPropertyGroup b, int index)
        {
            Console.Write("PropertyGroup #{0}", index);
            if (!string.IsNullOrEmpty(b.Condition))
                ConsoleNX.ColoredWriteLine(" when ({0})", ConsoleColor.Red, b.Condition);
            else
                Console.WriteLine();
            foreach (var px in b.Cast<BuildProperty>())
            {
                Console.Write("  {0} = {1}", px.Name, px.FinalValue);
                if (!string.IsNullOrEmpty(px.Condition))
                    ConsoleNX.ColoredWriteLine(" when ({0})", ConsoleColor.Red, px.Condition);
                else
                    Console.WriteLine();
            }
        }
    }
}

