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
using System.IO;
using System.Linq;
using NX;
using System.Diagnostics;
using Mono.Options;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Construction;
using System.Security.AccessControl;

namespace DNProj
{
    public class ItemProjectCommand : Command
    {
        string projName;
        Option<int> gIndex;

        public ItemProjectCommand()
            : base("dnproj item", 
                   @"show and edit build items.
in most cases, you can use 'dnproj add' and 'dnproj rm' instead.

example:
  $ dnproj item show
  $ dnproj item add -i 1 A.cs B.png:EmbeddedResource C.txt:None
  $ dnproj item add System.Core:Reference
  $ dnproj item set-condition -i 1 A.cs "" '\$(Platform)' == 'AnyCPU' ""
  $ dnproj item rm -i 1 B.png
  $ dnproj item add-group
  $ dnprij item set-group-condition -i 2 "" \$(MyCondition) == 'true' ""
  $ dnproj item rm-group -i 2

warning:
  on some shells such as bash, you must escape '$' charactors inside """" as ""\$"", or use '' instead.", "show and edit build items.", "<command>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
            Options.Add("i=|group-index=", "specify index of item group you want to show or edit. indices are shown as \n'ItemGroup #<index>'. [default=0]", i => gIndex = OptionNX.Option(int.Parse(i)));

            Commands["show"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["show"].LoadProject(ref args, ref projName);
                    var gs = Groups(p);
                    if (args.LengthNX() == 0 && !gIndex.HasValue)
                        foreach (var pg in gs.MapI((x, i) => new {v = x, i = i}))
                            printItemGroup(pg.v, pg.i);
                    else if (args.LengthNX() == 0)
                    {
                        var g = gs.Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                            {
                                Tools.FailWith("error: index out of range.");
                                return null;
                            });
                        printItemGroup(g, gIndex.Value);
                    } 
                }, "dnproj item show", "show project itemigurations.", "show project itemigurations.", "[options]");

            Commands["add"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["add"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                        {
                            Tools.FailWith("error: index out of range.");
                            return null;
                        });
                    if (args.LengthNX() < 1)
                        Tools.FailWith("error: missing parameter.");
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

            Commands["set-condition"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["set-condition"].LoadProject(ref args, ref projName);
                    var g = Groups(p).Try(xs => xs.Nth(gIndex.Value)).DefaultLazy(() =>
                        {
                            Tools.FailWith("error: index out of range.");
                            return null;
                        }); 
                    if (args.LengthNX() < 1)
                        Tools.FailWith("error: missing parameter.");
                    else
                    {
                        var name = args.First();
                        var val = args.LengthNX() > 1 ? args.Skip(1).JoinToString(" ") : "";

                        g.Cast<BuildItem>()
                         .Try(ys => ys.Find(x => x.Include == name))
                         .Match(
                            y => y.Condition = val,
                            () => Tools.FailWith("error: item with name '{0}' doesn't exist.", name)
                        );
                        printItemGroup(g, Groups(p).IndexOf(g));
                    }
                    p.Save(p.FullFileName);
                }, "dnproj item set-condition", "set conditon to item.\ngiving empty <condition> will remove condition.", "set condition to item.", "<filename>", "<condition>", "[options]");

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
                        g.Cast<BuildItem>()
                         .Try(xs => xs.First(x => x.Include == s))
                         .Match(
                            g.RemoveItem, 
                            () => Tools.FailWith("error: item with name '{0}' doesn't exist.", s)
                        );
                    printItemGroup(g, Groups(p).IndexOf(g));
                    p.Save(p.FullFileName);
                }, "dnproj item rm", "remove item.", "remove item.", "<filename>+", "[options]");

            Commands["add-group"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["add-group"].LoadProject(ref args, ref projName);
                    var cond = args.JoinToString(" ");
                    var g = p.AddNewItemGroup();
                    if (!string.IsNullOrEmpty(cond))
                        g.Condition = cond;
                    p.Save(p.FullFileName);
                }, "dnproj item add-group", "add item group.", "add item group.", "[condition]", "[options]");

            Commands["set-group-condition"] = new SimpleCommand(
                args =>
                {
                    var p = Commands["set-group-condition"].LoadProject(ref args, ref projName);
                    if (args.LengthNX() < 1)
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
                            p.RemoveItemGroup(g);
                            p.Save(p.FullFileName); 
                        },
                        () => Tools.FailWith("error: group index not specified.")
                    );
                }, "dnproj rm-group", "remove specified group. -i option is required.", "set condition to group.", "[options]");
        }

        IEnumerable<BuildItemGroup> Groups(Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>().Filter(x => !x.IsImported);
        }

        void printItemGroup(BuildItemGroup b, int index)
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
            }
        }
    }
}

