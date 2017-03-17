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
using NuGet;
using System.Runtime.Versioning;
using System.Reflection;


namespace DNProj
{
    public class NuGetListCommand : Command
    {
        string projName;
        Option<string> config;

        public NuGetListCommand()
            : base("dnproj nuget ls",
                   @"list installed NuGet packages.", "list installed NuGet packages.", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s.Some());

            this.AddTips("when --config is not used, dnproj will try to use a 'packages.config'\nin the same directory as your project file.");
        }

        public override void Run(IEnumerable<string> args)
        {
            var proj = this.LoadProject(ref args, ref projName);
            // find packages.config
            var conf = new PackageReferenceFile(config.Map(Path.GetFullPath)
                    .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")));

            if(!File.Exists(conf.FullPath))
                Report.Fatal("'{0}' does not exist.", conf);

            // find packages/
            var path = proj.ReferenceItems()
                .Filter(x => x.HasMetadata("HintPath"))
                .Map(x => new { Id = x.Include, Path = x.GetMetadata("HintPath") });

            if (!conf.GetPackageReferences().Any())
                Report.Fatal("there are no installed packages.");

            foreach(var r in conf.GetPackageReferences())
            {
                var i = path.Find(x => x.Path.Contains(r.Id));
                i.May(x => Console.WriteLine("{0}\n - {1}", r.Id, x.Path));
            }
        }
    }
}

