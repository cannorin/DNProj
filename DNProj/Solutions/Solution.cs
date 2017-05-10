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

namespace DNProj
{
    public interface IIndentedPrintable
    {
        IEnumerable<string> Print(int indent);
    }

    public static class SolutionTools
    {
        public static void Add<TK, TV>(this List<KeyValuePair<TK, TV>> l, TK key, TV value)
        {
            l.Add(new KeyValuePair<TK, TV>(key, value));
        }
    }

    public class SlnSection : IIndentedPrintable
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public List<KeyValuePair<string, string>> Items { get; set; }

        public SlnSection(bool isProjectSection, string name, string target)
        {
            Type = isProjectSection ? "Project" : "Global";
            Name = name;
            Target = target;
            Items = new List<KeyValuePair<string, string>>();
        }

        public IEnumerable<string> Print(int indent)
        {
            yield return string.Format("{0}Section({1}) = {2}", Type, Name, Target).Indent(indent, "\t");
            foreach(var i in Items)
                yield return string.Format("{0} = {1}", i.Key, i.Value).Indent(indent + 1, "\t");
            yield return string.Format("End{0}Section", Type).Indent(indent, "\t");
        }
    }

    public struct ProjectType
    {
        public readonly Guid Guid;

        public ProjectType(Guid g)
        {
            Guid = g;
        }

        public ProjectType(string guid)
        {
            Guid = Guid.Parse(guid);
        }

        public override string ToString()
        {
            return "{" + Guid.ToString().ToUpper() + "}";
        }

        public static readonly ProjectType CSharp = new ProjectType(Guid.Parse("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"));

        public static readonly ProjectType FSharp = new ProjectType(Guid.Parse("F2A71F9B-5D33-465A-A702-920D77279786"));

        public static readonly ProjectType SolutionItems = new ProjectType(Guid.Parse("2150E333-8FDC-42A3-9474-1A3956D46DE8"));

        public static readonly ProjectType Shared = new ProjectType(Guid.Parse("D954291E-2A0B-460D-934E-DC6B0785DB48"));
    }

    public class SlnProjectBlock : IIndentedPrintable
    {
        public ProjectType ProjectType { get; set; }
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public List<SlnSection> Sections { get; set; }

        public SlnProjectBlock(string name, string path, ProjectType t, Guid? guid = null)
        {
            Name = name;
            Path = path;
            ProjectType = t;
            Guid = guid ?? Guid.NewGuid();
            Sections = new List<SlnSection>();
        }

        public IEnumerable<string> Print(int indent)
        {
            yield return string.Format("{0}(\"{1}\") = \"{2}\", \"{3}\", \"{{{4}}}\"", "Project", ProjectType, Name, Path, Guid.ToString().ToUpper()).Indent(indent, "\t");  
            foreach(var s in Sections)
                foreach(var l in s.Print(indent + 1))
                yield return l;
            yield return "EndProject".Indent(indent, "\t");
        }
    }

    public class SlnGlobalBlock : IIndentedPrintable
    {
        public List<SlnSection> Sections { get; set; }

        public SlnGlobalBlock()
        {
            Sections = new List<SlnSection>();
        }

        public IEnumerable<string> Print(int indent)
        {
            yield return "Global".Indent(indent, "\t");
            foreach(var s in Sections)
                foreach(var l in s.Print(indent + 1))
                yield return l;
            yield return "EndGlobal".Indent(indent, "\t");
        }
    }
    public class Solution
    {
        public string FormatVersion { get; set; } // 12.00
        public string VisualStudioVersion { get; set; } // 2012
        public Dictionary<string, string> VSSettings { get; set; }
        public List<SlnProjectBlock> Projects { get; set; }
        public SlnGlobalBlock Global { get; set; }

        public Solution(string formatVersion = "12.00", string vsVersion = "2012", Dictionary<string, string> vssettings = null)
        {
            FormatVersion = formatVersion;
            VisualStudioVersion = vsVersion;
            VSSettings = vssettings ?? new Dictionary<string, string>();
            Projects = new List<SlnProjectBlock>();
            Global = new SlnGlobalBlock();
        }

        public IEnumerable<string> ToLines()
        {
            yield return "";
            yield return "Microsoft Visual Studio Solution File, Format Version " + FormatVersion;
            yield return "# Visual Studio " + VisualStudioVersion;
            foreach(var i in VSSettings)
                yield return i.Key + " = " + i.Value;
            foreach(var p in Projects)
                foreach(var l in p.Print(0))
                yield return l;
            foreach(var l in Global.Print(0))
                yield return l;
        }

        enum mode
        {
            root = 0,
            projblock = 1,
            projsect = 2,
            globlock = 3,
            glosect = 4
        }

        public static Solution Parse(string filename)
        {
            var s = new Solution();
            Option<SlnProjectBlock> pb = Option.None;
            Option<SlnSection> sc = Option.None;
            Option<SlnGlobalBlock> gb = Option.None;

            var currentMode = mode.root;

            var i = 0;
            foreach(var l in File.ReadAllLines(filename).Map(x => x.Replace("\t", "")))
            {
                i += 1;
                if(string.IsNullOrWhiteSpace(l))
                    continue;
                try
                {
                    switch(currentMode)
                    {
                        case mode.root:
                            if(l.StartsWith("Microsoft Visual Studio"))
                                s.FormatVersion = l.Split(' ').Last();
                            else if(l.StartsWith("# Visual Studio"))
                                s.VisualStudioVersion = l.Split(' ').Last();
                            else if(l.StartsWith("Project("))
                            {
                                var xs = l.Split(false, " = ");
                                var pt = xs[0].Split(true, "{", "}")[1];
                                var r = xs[1].Split(true, "\"", ", ");
                                pb = new SlnProjectBlock(r[0], r[1], new ProjectType(pt), Guid.Parse(r[2]));
                                currentMode = mode.projblock;
                            }
                            else if(l.StartsWith("Global"))
                            {
                                gb = new SlnGlobalBlock();
                                currentMode = mode.globlock;
                            }
                            else if(l.Contains(" = "))
                            {
                                var xs = l.Split(false, " = ");
                                s.VSSettings.Add(xs[0], xs[1]);
                            }
                            break;
                        case mode.projblock:
                            if(l.StartsWith("ProjectSection"))
                            {
                                var xs = l.Split(true, "(", ")", " = ");
                                sc = new SlnSection(true, xs[1], xs[2]);
                                currentMode = mode.projsect;
                            }
                            else if(l.StartsWith("EndProject"))
                            {
                                s.Projects.Add(pb.Value);
                                pb = Option.None;
                                currentMode = mode.root;
                            }
                            else
                                throw new InvalidDataException(string.Format("unexpected line '{0}' at line {1}", l, i));
                            break;
                        case mode.globlock:
                            if(l.StartsWith("GlobalSection"))
                            {
                                var xs = l.Split(true, "(", ")", " = ");
                                sc = new SlnSection(false, xs[1], xs[2]);
                                currentMode = mode.glosect;
                            }
                            else if(l.StartsWith("EndGlobal"))
                            {
                                s.Global = gb.Value;
                                currentMode = mode.root;
                            }
                            break;
                        case mode.projsect:
                        case mode.glosect:
                            if(l.Contains(" = "))
                            {
                                var xs = l.Split(false, " = ");
                                sc.Value.Items.Add(xs[0], xs[1]);
                            }
                            else if(l.StartsWith("End"))
                            {
                                if(currentMode == mode.projsect)
                                {
                                    pb.Value.Sections.Add(sc.Value);
                                    currentMode = mode.projblock;
                                }
                                else
                                {
                                    gb.Value.Sections.Add(sc.Value);
                                    currentMode = mode.globlock;
                                }
                                sc = Option.None;
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch
                {
                    throw new InvalidDataException(string.Format("unexpected line '{0}' at line {1}", l, i));

                }
            }
            return s;
        }
    }
} 

