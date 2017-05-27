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
using System.Linq;
using System.IO;
using NX;
using Microsoft.Build.BuildEngine;
using System.Collections.Generic;

namespace DNProj
{
    public static class ProjectTools
    {
        public static Tuple<BuildPropertyGroup, BuildPropertyGroup> AddDefaultConfigurations(this Project p, string arch = "AnyCPU")
        {
            string dc = "Debug|" + arch, rc = "Release|" + arch;
            var dccond = string.Format("'$(Configuration)|$(Platform)' == '{0}'", dc);
            var rccond = string.Format("'$(Configuration)|$(Platform)' == '{0}'", rc);
            var ps = p.PropertyGroups.Cast<BuildPropertyGroup>();
            var pgd = ps.Find(x => x.Condition.WeakEquals(dccond)).DefaultLazy(() =>
            {
                var pg = p.AddNewPropertyGroup(false);
                pg.Condition = dccond;
                pg.AddNewProperty("DebugSymbols", "true");
                pg.AddNewProperty("DebugType", "full");
                pg.AddNewProperty("Optimize", "false");
                pg.AddNewProperty("OutputPath", "bin\\Debug");
                pg.AddNewProperty("DefineConstants", "DEBUG;TRACE;");
                pg.AddNewProperty("ErrorReport", "prompt");
                pg.AddNewProperty("WarningLevel", "4");
                if (arch != "AnyCPU")
                    pg.AddNewProperty("PlatformTarget", arch);
                return pg;
            });

            var pgr = ps.Find(x => x.Condition.WeakEquals(rccond)).DefaultLazy(() =>
            {
                var pg = p.AddNewPropertyGroup(false);
                pg.Condition = rccond;
                pg.AddNewProperty("Optimize", "true");
                pg.AddNewProperty("OutputPath", "bin\\Release");
                pg.AddNewProperty("ErrorReport", "prompt");
                pg.AddNewProperty("WarningLevel", "4");
                if (arch != "AnyCPU")
                    pg.AddNewProperty("PlatformTarget", arch);
                return pg;
            });

            return Tuple.Create(pgd, pgr);
        }

        public static BuildItemGroup SourceItemGroup(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Find(x => !x.ToArray().Any() || x.ToArray().Any(b => Templates.BuildItems.Contains(b.Name)))
                .DefaultLazy(p.AddNewItemGroup);
        }

        public static BuildItemGroup ReferenceItemGroup(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Find(x => !x.ToArray().Any() || x.ToArray().Any(b => b.Name == "Reference"))
                .DefaultLazy(p.AddNewItemGroup); 
        }

        public static BuildPropertyGroup AssemblyPropertyGroup(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .First(x => string.IsNullOrEmpty(x.Condition));
        }

        public static BuildProperty DefaultConfiguration(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .SelectMany(x => x.Cast<BuildProperty>())
                .Find(x => x.Condition.Replace(" ", "") == "'$(Configuration)'==''")
                .DefaultLazy(() =>
            {
                var defcond = p.AssemblyPropertyGroup().AddNewProperty("Configuration", "Debug");
                defcond.Condition = " '$(Configuration)' == '' ";
                return defcond;
            });
        }

        public static BuildProperty DefaultTarget(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .SelectMany(x => x.Cast<BuildProperty>())
                .Find(x => x.Condition.Replace(" ", "") == "'$(Platform)'==''")
                .DefaultLazy(() =>
            {
                var defarch = p.AssemblyPropertyGroup().AddNewProperty("Platform", "AnyCPU");
                defarch.Condition = " '$(Platform)' == '' ";
                return defarch;
            });
        }

        public static BuildPropertyGroup DefaultDebugPropertyGroup(this Project p)
        {
            return p.AddDefaultConfigurations(p.DefaultTarget().Value).Item1;
        }

        public static BuildPropertyGroup DefaultReleasePropertyGroup(this Project p)
        {
            return p.AddDefaultConfigurations(p.DefaultTarget().Value).Item2;
        }

        public static IEnumerable<BuildItem> BuildItems(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Filter(xs => !xs.IsImported)
                .SelectMany(xs => xs.Cast<BuildItem>())
                .Filter(x => Templates.BuildItems.Contains(x.Name));
        }

        public static IEnumerable<BuildItem> ReferenceItems(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Filter(xs => !xs.IsImported)
                .SelectMany(xs => xs.Cast<BuildItem>())
                .Filter(x => x.Name == "Reference");
        }

        public static Option<string> GetAbsoluteHintPath(this BuildItem i)
        {
            if (i.HasMetadata("HintPath"))
                return new Uri(System.IO.Path.GetFullPath(i.GetMetadata("HintPath"))).AbsolutePath.Some();
            else
                return Option.None;
        }

        public static Option<Project> GetProject(string defaultName = null)
        {
            return Environment.CurrentDirectory
                .Try(x => defaultName ?? System.IO.Directory.GetFiles(x).First(f => f.EndsWith("proj")))
                .Try(x =>
            {
                var p = new Project();
                try
                {
                    p.Load(x, ProjectLoadSettings.IgnoreMissingImports);
                }
                catch (InvalidProjectFileException e)
                {
                    Report.Fatal("your project file {0} is corrupted. please fix it by yourself.\noriginal error:\n  {1}", x, e.Message);
                }
                return p;
            });
        }

        public static Project LoadProject(this Command c, ref IEnumerable<string> args, ref string projName)
        {
            args = c.Options.SafeParse(args);
            if (args.Any(Templates.HelpOptions.Contains))
            {
                c.Help(args);
                Environment.Exit(0);
            }
            return GetProject(projName)
                .AbortNone(() =>
            {
                Report.Error("project file not found.");
                c.Help(New.Seq(""));
                Environment.Exit(1);
            });
        }

        public static string GetName(string projPath)
        {
            return Path.GetFileNameWithoutExtension(projPath);
        }

        public static ProjectType GetProjectType(string projPath)
        {
            if(File.Exists(projPath))
            {
                var p = new Project();
                p.Load(projPath);
                if(p.BuildItems().Any(x => x.Include.EndsWith(".cs")) || p.Xml.Contains("Microsoft.CSharp"))
                    return ProjectType.CSharp;
                else if(p.BuildItems().Any(x => x.Include.EndsWith(".fs")) || p.Xml.Contains("Microsoft.FSharp"))
                    return ProjectType.FSharp;
                else if(p.Xml.Contains("Microsoft.CodeSharing"))
                    return ProjectType.Shared;
                else return ProjectType.CSharp;

            }
            else
            {
                if(projPath.EndsWith(".fsproj"))
                    return ProjectType.FSharp;
                else if(projPath.EndsWith(".shproj"))
                    return ProjectType.Shared;
                else
                    return ProjectType.CSharp;
            }
        }
    }
}
