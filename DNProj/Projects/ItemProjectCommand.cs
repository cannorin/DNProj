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
    public class ItemProjectCommand : Command
    {
        string projName;
        Option<int> gIndex;

        public ItemProjectCommand()
            : base("dnproj item", 
                   @"show and edit build items.
in most cases, you can use 'dnproj add', 'dnproj add-ref', 'dnproj rm', and 'dnproj rm-ref' instead.", "show and edit build items.", "<command>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
            Options.Add("i=|group-index=", "specify index of item group you want to show or edit. indices are shown as \n'ItemGroup #<index>'. [default=0]", i => gIndex = Option.Some(int.Parse(i)));

            @"  $ dnproj item show
  $ dnproj item add -i 1 A.cs B.png:EmbeddedResource C.txt:None
  $ dnproj item add System.Core:Reference
  $ dnproj item set-condition -i 1 A.cs "" '\$(Platform)' == 'AnyCPU' ""
  $ dnproj item set-hintpath System.Core ../packages/System.Core.1.0.0/lib/net45/System.Core.dll
  $ dnproj item rm -i 1 B.png
  $ dnproj item add-group
  $ dnprij item set-group-condition -i 2 "" \$(MyCondition) == 'true' ""
  $ dnproj item rm-group -i 2"
                .Split('\n').Iter(this.AddExample);

            this.AddWarning("you must escape the dollar sign '$' inside \"\" as \"\\$\", or use '' instead.");

            Commands["show"] = Child(
                args =>
                {
                    var p = Commands["show"].LoadProject(ref args, ref projName);
                    var gs = Groups(p);
                    if (args.Count() == 0 && !gIndex.HasValue)
                        foreach (var pg in gs.MapI((x, i) => new {v = x, i = i}))
                            printItemGroup(pg.v, pg.i);
                    else if (args.Count() == 0)
                    {
                    var g = gs.Nth(gIndex.Value).AbortNone(() =>
                            Report.Fatal("index out of range.")
                        );
                        printItemGroup(g, gIndex.Value);
                    } 
                }, "dnproj item show", "show project itemigurations.", "show project itemigurations.", "[options]");

            Commands["add"] = Child(
                args =>
                {
                    var p = Commands["add"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    );
                    if (!args.Any())
                        Report.Fatal("missing parameter.");
                    else
                        foreach (var f in args)
                        {
                            var fn = f;
                            var act = "Compile";
                            foreach (var x in new []{ "Compile", "EmbeddedResource", "None", "Reference" })
                                if (f.EndsWith(":" + x))
                                {
                                    act = x;
                                    fn = f.Replace(":" + x, "");
                                    break;
                                }
                            g.AddNewItem(act, fn);
                        }
                    printItemGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj item add", @"add files to specified project.
use <filename:buildaction> to specify build action.

build actions:
  Compile                    compile this file. (default)
  EmbeddedResource           embed this as resource.
  None                       do nothing.
  Reference                  treat as reference.", "add items.", "<filename[:buildaction]>+", "[options]");

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
                        var val = args.Count() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        g.Cast<BuildItem>()
                         .Find(x => x.Include == name)
                         .Match(
                            y => y.Condition = val,
                            () => Report.Fatal("item with name '{0}' doesn't exist.", name)
                        );
                        printItemGroup(g, Groups(p).IndexOf(g));
                    }
                    p.Save(p.FullFileName);
                }, "dnproj item set-condition", "set conditon to item.\ngiving empty <condition> will remove condition.", "set condition to item.", "<filename>", "<condition>", "[options]");

            Commands["set-hintpath"] = Child(
                args =>
                {
                    var p = Commands["set-hintpath"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    ); 
                    if (!args.Any())
                        Report.Fatal("missing parameter.");
                    else
                    {
                        var name = args.First();
                        var val = args.Count() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        g.Cast<BuildItem>()
                         .Find(x => x.Include == name)
                         .Match(
                            y =>
                            {
                                if (string.IsNullOrEmpty(val) && y.HasMetadata("HintPath"))
                                    y.RemoveMetadata("HintPath");
                                else
                                    y.SetMetadata("HintPath", val);
                            },
                            () => Report.Fatal("item with name '{0}' doesn't exist.", name)
                        );
                        printItemGroup(g, Groups(p).IndexOf(g));
                    }
                    p.Save(p.FullFileName);
                }, "dnproj item set-hintpath", "set hint path to item.\ngiving empty <path> will remove hint path.", "set hint path to item.", "<filename>", "<path>", "[options]");
            

            Commands["rm"] = Child(
                args =>
                {
                    var p = Commands["rm"].LoadProject(ref args, ref projName);
                var g = Groups(p).Nth(gIndex.Value).AbortNone(() =>
                        Report.Fatal("index out of range.")
                    ); 
                    foreach (var s in args)
                        g.Cast<BuildItem>()
                         .Find(x => x.Include == s)
                         .Match(
                            g.RemoveItem, 
                            () => Report.Fatal("item with name '{0}' doesn't exist.", s)
                        );
                    printItemGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj item rm", "remove item.", "remove item.", "<filename>+", "[options]");

            Commands["add-group"] = Child(
                args =>
                {
                    var p = Commands["add-group"].LoadProject(ref args, ref projName);
                    var cond = args.JoinToString(" ");
                    var g = p.AddNewItemGroup();
                    if (!string.IsNullOrEmpty(cond))
                        g.Condition = cond;
                    p.Save(p.FullFileName);
                }, "dnproj item add-group", "add item group.", "add item group.", "[condition]", "[options]");

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
                            p.RemoveItemGroup(g);
                            p.Save(p.FullFileName); 
                        },
                        () => Report.Fatal("group index not specified.")
                    );
                }, "dnproj rm-group", "remove specified group. -i option is required.", "set condition to group.", "[options]");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args, Option<string> incompleteInput = default(Option<string>))
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
                },
                incompleteInput: incompleteInput
            );
        }

        public override IEnumerable<CommandSuggestion> GetChildSuggestions(ChildCommand child, IEnumerable<string> args, Option<string> incompleteOption = default(Option<string>))
        {
            switch(child.Name.Split(' ').Last())
            {
                case "show":
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
                                  .Map(x => x.Cast<BuildItem>().Map(y => y.Include))
                                  .Map(CommandSuggestion.Values)
                                  .Default(CommandSuggestion.None);
                        }
                        else
                            yield return CommandSuggestion.None;
                    }
                    else
                        yield return CommandSuggestion.None;
                    break;

                case "set-hintpath":
                    if (!args.Any())
                    {
                        var p = ProjectTools.GetProject(projName);
                        if (p.HasValue)
                        {
                            var gs = Groups(p.Value);
                            var idx = gIndex.Default(0);
                            yield return 
                                gs.Nth(idx)
                                  .Map(x => x.Cast<BuildItem>().Map(y => y.Include))
                                  .Map(CommandSuggestion.Values)
                                  .Default(CommandSuggestion.None);
                        }
                        else
                            yield return CommandSuggestion.None;
                    }
                    else
                        yield return CommandSuggestion.Files("*.dll");
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
                                  .Map(x => x.Cast<BuildItem>().Map(y => y.Include))
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

        internal static void DumpItemGroup(Project p)
        {
            Groups(p).IterI(printItemGroup);
        }

        static IEnumerable<BuildItemGroup> Groups(Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>().Filter(x => !x.IsImported);
        }

        static void printItemGroup(BuildItemGroup b, int index)
        {
            Console.Write("ItemGroup #{0}", index);
            if (!string.IsNullOrEmpty(b.Condition))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" when ({0})", b.Condition);
                Console.ResetColor();
            }
            else
                Console.WriteLine();
            foreach (var px in b.Cast<BuildItem>())
            {
                Console.Write("  {1} ({0})", px.Name, px.Include);
                if (!string.IsNullOrEmpty(px.Condition))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(" when ({0})", px.Condition);
                    Console.ResetColor();
                }
                else
                    Console.WriteLine();
                if (px.HasMetadata("HintPath"))
                    Console.WriteLine("    HintPath: {0}", px.GetEvaluatedMetadata("HintPath"));
            }
        }
    }
}

