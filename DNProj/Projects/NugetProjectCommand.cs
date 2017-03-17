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

namespace DNProj
{
    public class NugetProjectCommand : Command
    {
        public NugetProjectCommand()
            : base("dnproj nuget", 
                   @"add or remove NuGet packages to/from specified project.
only the v2 API is supported (currently).",
                   "manage NuGet packages.", "<command>", "[options]")
        {
            Commands["search"] = new NuGetSearchCommand("dnproj nuget search");
            Commands["install"] = new NuGetInstallCommand();
            Commands["remove"] = new NuGetRemoveCommand();
            Commands["update"] = new NuGetUpdateCommand("dnproj nuget update");
            Commands["restore"] = new NuGetRestoreCommand("dnproj nuget restore");
            Commands["ls"] = new NuGetListCommand();

            @"  $ dnproj nuget search EntityFramework
  $ dnproj nuget install EntityFramework:5.0.0 --output-dir ../packages 
  $ dnproj nuget remove EntityFramework"
                .Split('\n').Iter(this.AddExample);
        }
    }

}

