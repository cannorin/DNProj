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
using NX;
using System.Diagnostics;
using Mono.Options;
using Microsoft.Build.BuildEngine;
using System.Threading;
using Microsoft.Build.Construction;
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
            var pgd = ps.Try(xs => xs.First(x => x.Condition == dccond)).DefaultLazy(() =>
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
                    return pg;
                });

            var pgr = ps.Try(xs => xs.First(x => x.Condition == rccond)).DefaultLazy(() =>
                {
                    var pg = p.AddNewPropertyGroup(false);
                    pg.Condition = rccond;
                    pg.AddNewProperty("Optimize", "true");
                    pg.AddNewProperty("OutputPath", "bin\\Release");
                    pg.AddNewProperty("ErrorReport", "prompt");
                    pg.AddNewProperty("WarningLevel", "4");
                    return pg;
                });

            return Tuple.Create(pgd, pgr);
        }

        public static BuildItemGroup SourceItemGroup(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Try(xs => xs.First(x => !x.ToArray().Any() || x.ToArray().Any(b => new []{ "Compile", "EmbeddedResource", "None" }.Contains(b.Name))))
                .DefaultLazy(() => p.AddNewItemGroup());
        }

        public static BuildItemGroup ReferenceItemGroup(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>()
                .Try(xs => xs.First(x => !x.ToArray().Any() || x.ToArray().Any(b => b.Name == "Reference")))
                .DefaultLazy(() => 　p.AddNewItemGroup()); 
        }

        public static BuildPropertyGroup AssemblyPropertyGroup(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .First(x => string.IsNullOrEmpty(x.Condition));
        }

        public static string DefaultConfiguration(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .Try(xs => xs.SelectMany(x => x.Cast<BuildProperty>()).First(x => x.Condition == " '$(Configuration)' == '' ").Value)
                .DefaultLazy(() =>
                {
                    var defcond = p.AssemblyPropertyGroup().AddNewProperty("Configuration", "Debug");
                    defcond.Condition = " '$(Configuration)' == '' ";
                    return "Debug";
                });
        }

        public static string DefaultTarget(this Project p)
        {
            return p.PropertyGroups.Cast<BuildPropertyGroup>()
                .Try(xs => xs.SelectMany(x => x.Cast<BuildProperty>()).First(x => x.Condition == " '$(Platform)' == '' ").Value)
                .DefaultLazy(() =>
                {
                    var defarch = p.AssemblyPropertyGroup().AddNewProperty("Platform", "AnyCPU");
                    defarch.Condition = " '$(Platform)' == '' ";
                    return "AnyCPU";
                });
        }

        public static BuildPropertyGroup DefaultDebugPropertyGroup(this Project p)
        {
            return p.AddDefaultConfigurations(p.DefaultTarget()).Item1;
        }

        public static BuildPropertyGroup DefaultReleasePropertyGroup(this Project p)
        {
            return p.AddDefaultConfigurations(p.DefaultTarget()).Item2;
        }

        public static IEnumerable<BuildItem> BuildItems(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>().SelectMany(xs => xs.Cast<BuildItem>()).Filter(x => new []{ "Compile", "EmbeddedResource", "None" }.Contains(x.Name));
        }

        public static IEnumerable<BuildItem> ReferenceItems(this Project p)
        {
            return p.ItemGroups.Cast<BuildItemGroup>().SelectMany(xs => xs.Cast<BuildItem>()).Filter(x => x.Name == "Reference");
        }

        public static Option<Project> GetProject(string defaultName)
        {
            return Environment.CurrentDirectory
                .Try(x => defaultName ?? System.IO.Directory.GetFiles(x).Find(f => f.EndsWith("proj")))
                .Map(x =>
                {
                    var p = new Project();
                    p.Load(x);
                    return p;
                });
        }
    }
}

