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

namespace DNProj
{
    public static class NuGetTools
    {
        public static IEnumerable<IPackage> ResolveDependencies(
            IPackage p, 
            FrameworkName fn, 
            IPackageRepository repo, 
            DependencyVersion dv = DependencyVersion.Lowest, 
            Option<IPackageConstraintProvider> cp = default(Option<IPackageConstraintProvider>))
        {
            var _cp = cp.Default(new DefaultConstraintProvider());
            return p.GetCompatiblePackageDependencies(fn)
                    .Map(x => repo.ResolveDependency(x, _cp, true, false, dv))
                    .Map(x => x.Singleton().Concat(ResolveDependencies(x, fn, repo, dv, _cp.Some())))
                    .Flatten()
                    .Distinct();
        }
    }


    public class NuGetLogger : ILogger, IFileConflictResolver
    {
        public int Indents { get; set; }

        public bool IsSilent { get; set; }

        public Option<Func<string, bool>> Filter { get; set; } 

        public NuGetLogger(int indents = 0, bool silent = false, Option<Func<string, bool>> filter = default(Option<Func<string, bool>>))
        {
            Indents = indents;
            IsSilent = silent;
            Filter = filter;
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            if (Filter.HasValue && (Filter.Value(string.Format(message, args)) == false))
                return;
            
            switch (level)
            {
                case MessageLevel.Error:
                    Seq.Repeat(' ', Indents).Iter(Console.Write);
                    ConsoleNX.ColoredWriteLine(message, ConsoleColor.Red, args);
                    break;
                case MessageLevel.Warning:
                    Seq.Repeat(' ', Indents).Iter(Console.Write);
                    ConsoleNX.ColoredWriteLine(message, ConsoleColor.Yellow, args);
                    break;
                default:
                    if (!IsSilent)
                    {
                        Seq.Repeat(' ', Indents).Iter(Console.Write);
                        Console.WriteLine(message, args);
                    }
                    break;
            }
        }

        public FileConflictResolution ResolveFileConflict(string message)
        {
            Report.Warning("ignoring file conflict ({0})...", message);
            return FileConflictResolution.IgnoreAll;
        }
    }
}

