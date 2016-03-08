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
using System.Xml.Serialization;

namespace DNProj
{
    public class RmProjectCommand : Command
    {
        string projName;

        public RmProjectCommand()
            : base("dnproj rm", "remove files from specified project.", "remove files.", "<filename>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var g = p.SourceItemGroup();
            foreach (var s in args)
                g.Cast<BuildItem>()
                    .Try(xs => xs.First(x => x.Include == s))
                    .Match(
                    g.RemoveItem, 
                    () => Tools.FailWith("error: item with name '{0}' doesn't exist.", s)
                );
            p.Save(p.FullFileName);
        }
    }
}

