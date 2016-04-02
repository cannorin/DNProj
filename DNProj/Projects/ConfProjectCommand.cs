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
                   @"show and edit project configurations.

example:
  $ dnproj conf show
  $ dnproj conf set -i 1 OutputPath bin/Debug
  $ dnproj conf set-condition Platform "" '\$(Platform)' == '' ""
  $ dnproj conf rm OutputPath -i 1
  $ dnproj conf add-group
  $ dnprij conf set-group-condition -i 2 "" \$(MyCondition) == 'true' ""
  $ dnproj conf rm-group -i 2

warning:
  on some shells such as bash, you must escape '$' charactors inside """" as ""\$"", or use '' instead.", "show and edit project configurations.", "<command>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
            Options.Add("i=|group-index=", "specify index of property group you want to show or edit. indices are shown as \n'PropertyGroup #<index>'. [default=0]", i => gIndex = Option.Some(int.Parse(i)));

            Commands["show"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["show"].LoadProject(ref args, ref projName);
                    var gs = Groups(p);
                    if (args.LengthNX() == 0 && !gIndex.HasValue)
                        foreach (var pg in gs.MapI((x, i) => new {v = x, i = i}))
                            printPropertyGroup(pg.v, pg.i);
                    else if (args.LengthNX() == 0)
                    {
                        var g = gs.Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                        {
                            Tools.FailWith("error: index out of range.");
                            return null;
                        });
                        printPropertyGroup(g, gIndex.Value);
                    } 
                }, "dnproj conf show", "show project configurations.", "show project configurations.", "[options]");

            Commands["set"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["set"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                    {
                        Tools.FailWith("error: index out of range.");
                        return null;
                    });
                    if (!args.Any())
                        Tools.FailWith("error: missing parameter.");
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

            Commands["set-condition"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["set-condition"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                    {
                        Tools.FailWith("error: index out of range.");
                        return null;
                    }); 
                    if (!args.Any())
                        Tools.FailWith("error: missing parameter.");
                    else
                    {
                        var name = args.First();
                        var val = args.LengthNX() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        g.Cast<BuildProperty>()
                                .Find(x => x.Name == name)
                                .Match(
                            y => y.Condition = val,
                            () => Tools.FailWith("error: property with name '{0}' doesn't exist.", name)
                        );
                        printPropertyGroup(g, Groups(p).IndexOf(g));
                    }
                    p.Save(p.FullFileName);
                }, "dnproj conf set-condition", "set conditon to property.\ngiving empty <condition> will remove condition.", "set condition to property.", "<name>", "<condition>", "[options]");

            Commands["rm"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["rm"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                    {
                        Tools.FailWith("error: index out of range.");
                        return null;
                    }); 
                    foreach (var s in args)
                    {
                        if (g.Cast<BuildProperty>().Any(x => x.Name == s))
                            g.RemoveProperty(s);
                        else
                            Tools.FailWith("error: property with name '{0}' doesn't exist.", s);
                    }
                    printPropertyGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj conf rm", "remove property.", "remove property.", "<name>+", "[options]");

            Commands["add-group"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["add-group"].LoadProject(ref args, ref projName);
                    var cond = args.JoinToString(" ");
                    var g = p.AddNewPropertyGroup(false);
                    if (!string.IsNullOrEmpty(cond))
                        g.Condition = cond;
                    p.Save(p.FullFileName);
                }, "dnproj conf add-group", "add property group.", "add property group.", "[condition]", "[options]");

            Commands["set-group-condition"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["set-group-condition"].LoadProject(ref args, ref projName);
                    if (!args.Any())
                        Tools.FailWith("error: missing parameter.");

                    var cond = args.JoinToString(" ");
                    gIndex.Map(Groups(p).Nth)
                    .Match(
                        g =>
                        {
                            if (!string.IsNullOrEmpty(cond))
                                g.Condition = cond;
                            p.Save(p.FullFileName); 
                        },
                        () => Tools.FailWith("error: group index not specified.")
                    );
                }, "dnproj set-group-condition", "set condition to specified group. -i option is required. \ngiving empty condition will remove condition.", "set condition to group.", "[condition]", "[options]");

            Commands["rm-group"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["rm-group"].LoadProject(ref args, ref projName);

                    gIndex.Map(Groups(p).Nth)
                    .Match(
                        g =>
                        {
                            p.RemovePropertyGroup(g);
                            p.Save(p.FullFileName); 
                        },
                        () => Tools.FailWith("error: group index not specified.")
                    );
                }, "dnproj rm-group", "remove specified group. -i option is required.", "set condition to group.", "[options]");
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

