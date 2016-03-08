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
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class EditProjectCommand : Command
    {
        string projName;
        string editor;

        public EditProjectCommand()
            : base("dnproj edit", "edit files with $EDITOR.\nyou can add, remove, and reorder multiple files at once.", "edit files with editor.", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
            Options.Add("e=|editor=", "specify editor.", e => editor = e);
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var s = "#References:" + Environment.NewLine
                    + p.ReferenceItemGroup().Cast<BuildItem>().Map(x => 
                        string.Format("{0} {1}", x.Name, x.Include)
                    + (string.IsNullOrEmpty(x.Condition) ? "" : string.Format(" Condition=\"{0}\"", x.Condition))
                    + (x.HasMetadata("HintPath") ? string.Format(" HintPath=\"{0}\"", x.GetMetadata("HintPath")) : "")
                    ).JoinToString(Environment.NewLine)
                    + Environment.NewLine
                    + "#Files:" + Environment.NewLine
                    + p.SourceItemGroup().Cast<BuildItem>().Map(x =>
                        string.Format("{0} {1}", x.Name, x.Include)
                    ).JoinToString(Environment.NewLine)
                    + Environment.NewLine;
            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, s);
            var ed = editor ?? Shell.GetUnixEnvironmentVariable("EDITOR");
            Shell.Execute(ed, temp);
            var s2 = File.ReadAllText(temp);
            File.Delete(temp);
            p.RemoveItemGroup(p.SourceItemGroup());
            p.RemoveItemGroup(p.ReferenceItemGroup());
            var l1 = s.Split(true, Environment.NewLine);
            var l2 = s2.Split(true, Environment.NewLine);
            foreach (var x in l2)
            {
                var xs = x.Split(' ');
                if (x.StartsWith("#"))
                {
                }
                else if (xs.Length < 2)
                    Tools.FailWith("error: invalid line '{0}'.", x);
                else
                {
                    if (Templates.BuildItems.Contains(xs[0]))
                    {
                        var i = p.SourceItemGroup().AddNewItem(xs[0], xs[1]);
                        if (x.Contains("HintPath="))
                        {
                            var hp = x.Split(true, "HintPath=")[1].Split(true, " Condition=")[0];
                            i.SetMetadata("HintPath", hp.Substring(1, hp.Length - 2));
                        }
                        if (x.Contains("Condition="))
                        {
                            var cd = x.Split(true, "Condition=")[1].Split(true, " HintPath=")[0];
                            i.Condition = cd.Substring(1, cd.Length - 2);
                        }
                    }
                    else if (xs[0] == "Reference")
                    {
                        var i = p.ReferenceItemGroup().AddNewItem(xs[0], xs[1]);
                        if (x.Contains("HintPath="))
                        {
                            var hp = x.Split(true, "HintPath=")[1].Split(true, " Condition=")[0];
                            i.SetMetadata("HintPath", hp.Substring(1, hp.Length - 2));
                        }
                        if (x.Contains("Condition="))
                        {
                            var cd = x.Split(true, "Condition=")[1].Split(true, " HintPath=")[0];
                            i.Condition = cd.Substring(1, cd.Length - 2);
                        }
                    }
                    else
                        Tools.FailWith("error: invalid line '{0}'.", x);
                }
            }
            var diff = l1.Diff(l2);
            if (diff.Items.Any(x => x.State != DiffState.NoChange))
            {
                diff.Print();
                p.Save(p.FullFileName);
            }
            else
                Console.WriteLine("(no changes made)");
        }
    }
}

