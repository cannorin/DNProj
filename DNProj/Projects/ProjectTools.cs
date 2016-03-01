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

namespace DNProj
{
    public static class ProjectTools
    {
        public static Tuple<BuildPropertyGroup, BuildPropertyGroup> AddDefaultConfigurations(Project p, string arch = "AnyCPU")
        {
            string dc = "Debug|" + arch, rc = "Release|" + arch;
            var dccond = string.Format("'$(Configuration)|$(Platform)' == '{0}'", dc);
            var rccond = string.Format("'$(Configuration)|$(Platform)' == '{0}'", rc);
            var ps = p.PropertyGroups.Cast<BuildPropertyGroup>();
            var pgd = ps.Try(xs => xs.First(x => x.Condition == dccond)).Match(x => x, () =>
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

            var pgr = ps.Try(xs => xs.First(x => x.Condition == rccond)).Match(x => x, () =>
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
    }
}

