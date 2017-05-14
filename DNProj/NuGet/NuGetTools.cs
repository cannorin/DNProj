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

        public DNPackageManager(IPackageRepository repo, string path, bool verbose = false)
            : base(repo, path)
        {
            this.Logger = new NuGetLogger();
            this.Verbose = verbose;
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

        public bool Verbose { get; set; }

        public new event EventHandler<DNPackageOperationEventArgs> PackageInstalled;

        public new event EventHandler<DNPackageOperationEventArgs> PackageInstalling;

        public Option<IPackage> InstallPackageWithValidation(FrameworkName fn, string id, Option<string> version = default(Option<string>), bool allowPre = false, bool force = false)
        {
            return InstallPackageWithValidation(fn, id, version.Map(SemanticVersion.Parse), allowPre, force);
        }

        public Option<IPackage> InstallPackageWithValidation(FrameworkName fn, string id, Option<SemanticVersion> version, bool allowPre = false, bool force = false)
        {
            var repo = this.SourceRepository;
            if(Verbose)
                Report.WriteLine(this.Logger.Indents, "* resolving '{0}'...", id);

            var p = version.Match(
                v => repo.FindPackage(id, v),
                () => repo.FindPackagesById(id).First(x => x.IsLatestVersion)
            );

            if (p == null)
            {
                Report.Error(this.Logger.Indents, "package '{0}' doesn't exists.", id);
                return Option.None;
            }

            var sfs = p.GetSupportedFrameworks();

            if(Verbose && p.IsSatellitePackage())
                Report.Info(this.Logger.Indents, "package '{0}' is a satellite package.", id); 
            else if(!sfs.Any())
            {
                if(Verbose) 
                    Report.Warning(this.Logger.Indents, "package '{0}' has an empty target framework.", id); 
            }
            else if (!force && !sfs.Contains(fn))
            {
                var alto = p.FindLowerCompatibleFramework(fn);    
                if (alto.HasValue)
                {
                    alt = alto;
                    if(Verbose)
                        Report.Warning(this.Logger.Indents, "package '{0}' doesn't support framework '{1}', using lower compatible version '{2}'...", id, fn, alto.Value);
                }
                else if(sfs.Any(x => x.IsPortableFramework()))
                {
                    
                    var psf = 
                        sfs.Filter(x => x.IsPortableFramework())
                            .Map(x => new 
                            { 
                                Framework = x, 
                                Profile = NetPortableProfile.Parse(NetPortableProfileTable.Instance, x.Profile, true) 
                            })
                            .Find(x => NuGetTools.FindLowerCompatibleFramework(x.Profile.SupportedFrameworks, fn).HasValue);
                    if (psf.HasValue)
                    {
                        alt = psf.Value.Framework;
                        if(Verbose)
                            Report.Info(this.Logger.Indents, "package '{0}' is a PCL package, using profile '{1}'.", id, psf.Value.Profile.Name);  
                    }
                    else
                    {
                        Report.Error(this.Logger.Indents, "package '{0}' is a PCL package, but there is no profile compatible with framework '{1}'.", id, fn.FullName);
                        Report.Error(this.Logger.Indents, "available profiles are: {0}", sfs.Filter(x => x.IsPortableFramework()).Map(x => x.Profile).JoinToString(", "));
                        Report.Info(this.Logger.Indents, "'--force' to ignore this error, if needed.");
                        return Option.None;
                    }
                }
                else
                {
                    Report.Error(this.Logger.Indents, "package '{0}' doesn't support framework '{1}'.", id, fn.FullName);
                    Report.Error(this.Logger.Indents, "available frameworks are: {0}", sfs.Map(x => string.Format("'{0}'", x)).JoinToString(", "));
                    NuGetTools.FindUpperCompatibleFramework(p, fn)
                        .Match
                        (
                            uf => 
                            {
                                Report.Info(this.Logger.Indents, "consider upgrading target framework to '{0}'.", uf);
                                Report.Info(this.Logger.Indents, "> 'dnproj conf set TargetFrameworkVersion \"v{0}\"'", uf.Version);
                            },
                            () => Report.Info(this.Logger.Indents, "'--force' to ignore this error, if needed.")
                        );
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

        public static Option<FrameworkName> FindLowerCompatibleFramework(this IPackage p, FrameworkName fn)
        {
            var sfs = p.GetSupportedFrameworks();
            return FindLowerCompatibleFramework(sfs, fn);
        }

        public static Option<FrameworkName> FindLowerCompatibleFramework(IEnumerable<FrameworkName> sfs, FrameworkName fn)
        {
            if (!sfs.Contains(fn))
            {
                return sfs.Find(x => 
                    x.Identifier == fn.Identifier
                && x.Version.Major == fn.Version.Major
                && x.Version.Minor <= fn.Version.Minor
                && x.Version.Build <= fn.Version.Build
                );
            }
            else
                return fn.Some();
        }

        public static IEnumerable<FrameworkName> GetLowerCompatibleFrameworks(IEnumerable<FrameworkName> sfs, FrameworkName fn)
        {
            if (!sfs.Contains(fn))
                return sfs.Filter(x => 
                    x.Identifier == fn.Identifier 
                    && x.Version.Major == fn.Version.Major 
                    && x.Version.Minor <= fn.Version.Minor 
                    && x.Version.Build <= fn.Version.Build
                );
            else
                return fn.Singleton();
        }

        public static Option<FrameworkName> FindUpperCompatibleFramework(this IPackage p, FrameworkName fn)
        {
            var sfs = p.GetSupportedFrameworks();
            return FindUpperCompatibleFramework(sfs, fn);
        }

        public static Option<FrameworkName> FindUpperCompatibleFramework(IEnumerable<FrameworkName> sfs, FrameworkName fn)
        {
            if (!sfs.Contains(fn))
                return sfs.Find(x => 
                    x.Identifier == fn.Identifier 
                    && x.Version.Major == fn.Version.Major 
                    && x.Version.Minor >= fn.Version.Minor 
                    && x.Version.Build >= fn.Version.Build
                );
            else
                return fn.Some();
        }

        public static IEnumerable<FrameworkName> GetUpperCompatibleFramework(IEnumerable<FrameworkName> sfs, FrameworkName fn)
        {
            if (!sfs.Contains(fn))
                return sfs.Filter(x => 
                    x.Identifier == fn.Identifier
                 && x.Version.Major == fn.Version.Major
                 && x.Version.Minor >= fn.Version.Minor
                 && x.Version.Build >= fn.Version.Build
                );
            else
                return fn.Singleton();
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
                File.AppendAllLines(p, ps.Map(x => x.Id).FilterOut(ls.Contains));
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
            if (Filter.Check(x => x(string.Format(message, args)) == false))
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

