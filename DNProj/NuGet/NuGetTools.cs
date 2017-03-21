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
using Microsoft.Build.BuildEngine;
using NuGet;
using System.Runtime.Versioning;

namespace DNProj
{
    public class DNPackageOperationEventArgs : PackageOperationEventArgs
    {
        public DNPackageOperationEventArgs(IPackage p, IFileSystem f, string path, Option<FrameworkName> v)
            : base(p, f, path)
        {
            AlternativeFramework = v;
        }

        public Option<FrameworkName> AlternativeFramework { get; set; }
    }

    public class DNPackageManager : PackageManager
    {
        Option<FrameworkName> alt;

        public DNPackageManager(IPackageRepository repo, string path)
            : base(repo, path)
        {
            this.Logger = new NuGetLogger();
            this.PackageInstalled += (sender, e) => {};
            this.PackageInstalling += (sender, e) => {};
            base.PackageInstalled += (sender, e) => 
            {
                this.SourceRepository.UpdatePackageNamesCache(e.Package.Singleton());
                var _e = new DNPackageOperationEventArgs(e.Package, e.FileSystem, e.InstallPath, alt);
                this.PackageInstalled(sender, _e);
            };
            base.PackageInstalling += (sender, e) => 
            {
                var _e = new DNPackageOperationEventArgs(e.Package, e.FileSystem, e.InstallPath, alt);
                this.PackageInstalling(sender, _e);
            };
        }

        public new NuGetLogger Logger
        {
            get
            {
                return (NuGetLogger)base.Logger;
            }
            set
            {
                base.Logger = value;
            }
        }

        public new event EventHandler<DNPackageOperationEventArgs> PackageInstalled;

        public new event EventHandler<DNPackageOperationEventArgs> PackageInstalling;

        public Option<IPackage> InstallPackageWithValidation(FrameworkName fn, string id, Option<string> version = default(Option<string>), bool allowLowerFw = false, bool allowPre = false)
        {
            return InstallPackageWithValidation(fn, id, version.Map(SemanticVersion.Parse), allowLowerFw, allowPre);
        }

        public Option<IPackage> InstallPackageWithValidation(FrameworkName fn, string id, Option<SemanticVersion> version, bool allowLowerFw = false, bool allowPre = false)
        {
            var repo = this.SourceRepository;
            Report.WriteLine(this.Logger.Indents, "* resolving '{0}'...", id);

            var p = version.Match(
                        v => repo.FindPackage(id, v),
                        () => repo.FindPackage(id)
                    );

            if (p == null)
            {
                Report.Error(this.Logger.Indents, "package '{0}' doesn't exists.", id);
                return Option.None;
            }

            var sfs = p.GetSupportedFrameworks();

                
            if (!sfs.Contains(fn))
            {
                var alto = p.FindAlternativeFramework(fn);    
                if (alto.HasValue)
                {
                    if (allowLowerFw)
                    {
                        alt = alto;
                        Report.Warning(this.Logger.Indents, "package '{0}' doesn't support '{1}', installing '{2}' instead...", id, fn, alto.Value);
                    }
                    else
                    {
                        Report.Error(this.Logger.Indents, "package '{0}' doesn't support framework '{1}'.", id, fn.FullName);
                        Report.Info(this.Logger.Indents, "'--allow-downgrade-framework' to install '{0}' version instead.", alto.Value);
                        return Option.None;
                    }
                }
                else
                {
                    Report.Error(this.Logger.Indents, "package '{0}' doesn't support framework '{1}'.", id, fn.FullName);
                    Report.Error(this.Logger.Indents, "available frameworks are: {0}", sfs.Map(x => string.Format("'{0}'", x)).JoinToString(", "));
                    return Option.None;
                }
            }

            Report.WriteLine(this.Logger.Indents, "* installing '{0}'...", p.GetFullName());
            this.Logger.Indents += 2;
            this.InstallPackage(p, true, allowPre);
            this.Logger.Indents -= 2;
            return p.Some();
        }
    }

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

        public static Option<FrameworkName> FindAlternativeFramework(this IPackage p, FrameworkName fn)
        {
            var sfs = p.GetSupportedFrameworks();

            if (!sfs.Contains(fn))
                return sfs.Find(x => 
                    x.Identifier == fn.Identifier && x.Version.Major == fn.Version.Major
                );
            else
                return fn.Some();
        }

        public static string GetRepositoryCachePath(this IPackageRepository repo)
        {
            var n = repo.Source.Replace(":", "_").Replace("/", "_").Replace(":", "_");
            return Path.Combine(Path.GetTempPath(), n + ".cache");
        }

        public static void UpdatePackageNamesCache(this IPackageRepository repo, IEnumerable<IPackage> ps)
        {
            try
            {
                var p = repo.GetRepositoryCachePath();
                if (!File.Exists(p))
                    File.Create(p).Close();
                var ls = File.ReadAllLines(p);
                File.AppendAllLines(p, ps.Map(x => x.Id).Filter(x => !ls.Contains(x)));
            }
            catch {}
        }

        public static string[] GetCachedPackageNames(this IPackageRepository repo)
        {
            var p = repo.GetRepositoryCachePath();
            if (!File.Exists(p))
                return new string[] { };
            else
                return p.Try(x => File.ReadAllLines(x)).Default(new string[]{ });
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

