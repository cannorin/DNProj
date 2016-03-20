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
using System.Xml.Linq;

namespace DNProj
{
    public class EditProjectCommand : Command
    {
        string projName;
        Option<string> editor;

        public EditProjectCommand()
            : base("dnproj edit", "edit files and references with $EDITOR.\nyou can add, remove, and reorder multiple items at once.", "edit files and references with editor.", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
            Options.Add("e=|editor=", "specify editor.", e => editor = e.Some());
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var s = StringNX.Build(sb =>
                {
                    sb.WriteLine("# lines starts with '#' will be ignored.");
                    sb.WriteLine("# lines ends with '\\' will be joined to the next line.");
                    sb.WriteLine("# example:");
                    sb.WriteLine("#   Compile Program.cs");
                    sb.WriteLine("#   Reference System.Hoge \\\n#     Condition='$(Platform)' == 'x86' \\\n#     HintPath=./packages/test");
                    sb.WriteLine();
                    sb.WriteLine("# references:");
                    foreach (var x in p.ReferenceItemGroup().Cast<BuildItem>())
                    {
                        sb.Write("{0} {1}", x.Name, x.Include);
                        if (!string.IsNullOrEmpty(x.Condition))
                            sb.Write(" \\\n  Condition={0}", x.Condition);
                        if (x.HasMetadata("HintPath"))
                            sb.Write(" \\\n  HintPath={0}", x.GetMetadata("HintPath"));
                        sb.WriteLine();
                    }
                    sb.WriteLine();
                    sb.WriteLine("# files:");
                    foreach (var x in p.SourceItemGroup().Cast<BuildItem>())
                        sb.WriteLine("{0} {1}", x.Name, x.Include);	
                });
            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, s);
            var ed = (editor || Shell.GetUnixEnvironmentVariable("EDITOR").Some())
				.Match(x =>
                {
                    if (string.IsNullOrEmpty(x))
                        Tools.FailWith("error: $EDITOR has not been set");
                    return x;
                }, () =>
                {
                    Tools.FailWith("error: $EDITOR has not been set");
                    return null;
                });
            Shell.Execute(ed, temp);
            var s2 = File.ReadAllText(temp);
            File.Delete(temp);
            p.RemoveItemGroup(p.SourceItemGroup());
            p.RemoveItemGroup(p.ReferenceItemGroup());
            var l1 = s.Split(true, Environment.NewLine);
            var l2 = s2.Split(true, Environment.NewLine);
            string t = "";
            foreach (var x in l2)
            {
                if (x.EndsWith("\\"))
                {
                    t += x.Substring(0, x.Length - 1);
                    continue;
                }
                t += x;
                var xs = t.Split(' ');
                if (t.StartsWith("#") || string.IsNullOrEmpty(t))
                {
                }
                else if (xs.Length < 2)
                    Tools.FailWith("error: invalid line '{0}'.", t);
                else
                {
                    if (Templates.BuildItems.Contains(xs[0]))
                    {
                        var i = p.SourceItemGroup().AddNewItem(xs[0], xs[1]);
                        if (t.Contains("HintPath="))
                        {
                            var hp = t.Split(true, "HintPath=")[1].Split(true, "Condition=")[0];
                            i.SetMetadata("HintPath", hp.RemoveHeadTailSpaces());
                        }
                        if (t.Contains("Condition="))
                        {
                            var cd = t.Split(true, "Condition=")[1].Split(true, "HintPath=")[0];
                            i.Condition = cd.RemoveHeadTailSpaces();
                        }
                    }
                    else if (xs[0] == "Reference")
                    {
                        var i = p.ReferenceItemGroup().AddNewItem(xs[0], xs[1]);
                        if (t.Contains("HintPath="))
                        {
                            var hp = t.Split(true, "HintPath=")[1].Split(true, "Condition=")[0];
                            i.SetMetadata("HintPath", hp.RemoveHeadTailSpaces());
                        }
                        if (t.Contains("Condition="))
                        {
                            var cd = t.Split(true, "Condition=")[1].Split(true, "HintPath=")[0];
                            i.Condition = cd.RemoveHeadTailSpaces();
                        }
                    }
                    else
                        Tools.FailWith("error: invalid line '{0}'.", t);
                }
                t = "";
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

