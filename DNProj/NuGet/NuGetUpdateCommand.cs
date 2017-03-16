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
    public class NuGetUpdateCommand : Command
    {
        string projName;
        Option<string> config;
        string sourceUrl = "https://packages.nuget.org/api/v2";
        bool verbose = false;
        bool allow40 = false;
        bool recursive = false;

        public NuGetUpdateCommand(string name)
            : base(name,
                   @"update NuGet packages.
                
if no package names are specified, it will (try to) update all the installed packages", "update NuGet packages.", "[name]*", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s.Some());
            Options.Add("r|recursive", "update packages' dependencies too.", _ => recursive = _ != null);
            Options.Add("a|allow-downgrade-framework", "try installing .NET 4.0 version if the package doesn't support .NET 4.5.", _ => allow40 = _ != null);
            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);
        }

        public override void Run(IEnumerable<string> args)
        {
            try
            {
                var proj = this.LoadProject(ref args, ref projName);

                // resolve pathes

                var conf = new PackageReferenceFile(config.Map(Path.GetFullPath)
                    .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")));
                var path = proj.ReferenceItems()
                    .Map(ProjectTools.GetAbsoluteHintPath)
                    .FindSome()
                    .Map(x => Path.GetDirectoryName(x).Unfold(s => Tuple.Create(Directory.GetParent(s), Directory.GetParent(s).FullName)).Nth(2))
                    .Map(x => x.FullName).AbortNone(() =>
                        Report.Fatal("there are no installed packages.")
                           );

                // read proj to get fn
                var apg = proj.AssemblyPropertyGroup().Cast<BuildProperty>();
                var tfi = apg.Find(x => x.Name == "TargetFrameworkIdentifier").Map(x => x.Value).DefaultLazy(() =>
                {
                    Report.Warning("TargetFrameworkIdentifier has not been set, using '.NETFramework'...");
                    Report.Info("if needed, you can change it by 'dnproj conf set TargetFrameworkIdentifier <identifier>'.");
                    return ".NETFramework";
                });

                var tfv = apg.Find(x => x.Name == "TargetFrameworkVersion").Map(x => x.Value).DefaultLazy(() =>
                {
                    Report.Warning("TargetFrameworkVersion has not been set, using 'v4.5'...");
                    Report.Info("if needed, you can change it by 'dnproj conf set TargetFrameworkVersion <version>'.");
                    return "v4.5";
                });
                var fn = new FrameworkName(tfi, Version.Parse(tfv.Replace("v", "")));

                // prepare nuget things
                var repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl);
                var localrepo = new LocalPackageRepository(path);
                var pm = new PackageManager(repo, path);
                var logger = new NuGetLogger(2, !verbose);
                pm.Logger = logger;
                pm.DependencyVersion = DependencyVersion.Lowest;
                pm.SkipPackageTargetCheck = true;
                pm.PackageUninstalling += (sender, e) =>
                {
                    e.Package.AssemblyReferences
                        .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                        .Find(x => x.SupportedFrameworks.Contains(fn) || (allow40 && x.TargetFramework.Identifier == fn.Identifier && x.TargetFramework.Version.Major == fn.Version.Major))
                        .Match(
                            a =>
                            {
                                conf.DeleteEntry(e.Package.Id, e.Package.Version);
                            }, 
                            () => 
                            {
                                Report.Warning(logger.Indents, "newer version of '{0}' doesn't support framework '{1}', cancelling...", e.Package.Id, fn);
                                e.Cancel = true;
                            }
                        );
                };

                pm.PackageInstalling += (sender, e) =>
                {
                    e.Package.AssemblyReferences
                        .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                        .Find(x => x.SupportedFrameworks.Contains(fn) || (x.TargetFramework.Identifier == fn.Identifier && x.TargetFramework.Version.Major == fn.Version.Major))
                        .Match(
                            a => 
                            {
                                if(!allow40)
                                {
                                    Report.Info(logger.Indents, "'--allow-downgrade-framework' to update to '{0}', which supports '{1}'.", e.Package.Version, a.TargetFramework);
                                    e.Cancel = true;
                                }
                            }, 
                            () => 
                            {
                                e.Cancel = true;
                            }
                        );
                };

                pm.PackageInstalled += (sender, e) =>
                {
                    var pn = e.Package.GetFullName().Replace(' ', '.');
                    var bp = new Uri(proj.FullFileName);
                    e.Package.AssemblyReferences
                        .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                        .Find(x => x.SupportedFrameworks.Contains(fn) || (allow40 && x.TargetFramework.Identifier == fn.Identifier && x.TargetFramework.Version.Major == fn.Version.Major))
                        .Match(
                            a =>
                            {
                                conf.AddEntry(e.Package.Id, e.Package.Version, false, fn);
                                var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                                var rp = bp.MakeRelativeUri(absp).ToString();
                                var an = Assembly.LoadFile(absp.AbsolutePath).GetName().Name;
                                proj.ReferenceItems()
                                    .Filter(x => x.HasMetadata("HintPath"))
                                    .Find(x => x.Include == an)
                                    .Match(
                                        i =>
                                        {
                                            if(verbose)
                                                Report.WriteLine(logger.Indents, "Replacing existing reference to '{0}, HelpPath={1}' with '{2}, HelpPath={3}'...", i.Include, i.GetMetadata("HintPath"), an, rp);
                                            proj.ReferenceItemGroup().RemoveItem(i);
                                            var j = proj.ReferenceItemGroup().AddNewItem("Reference", an);
                                            j.SetMetadata("HintPath", rp);
                                        },
                                        () => {}
                                );
                            }, 
                            () => {}
                        );
                };

                // update
                if (args.Any())
                {
                    foreach (var name in args)
                    {
                        if (!localrepo.Exists(name))
                        {
                            Report.Error("package '{0}' is not installed.", name);
                            break;
                        }
                        Console.WriteLine("* updating '{0}'...", name);
                        pm.UpdatePackage(name, false, false);

                        if(recursive)
                        {
                            var p = localrepo.FindPackage(name);
                            var pkgs = NuGetTools.ResolveDependencies(p, fn, repo);
                            logger.Indents += 2;
                            foreach(var pkg in pkgs)
                            {
                                Console.WriteLine("  * updating depending package '{0}'...", pkg.GetFullName());
                                pm.UpdatePackage(pkg.Id, false, false);
                            }
                            logger.Indents -= 2;
                        }
                    }
                }
                else
                {
                    foreach (var pr in conf.GetPackageReferences())
                    {
                        Console.WriteLine("* updating '{0}'...", pr.Id);
                        pm.UpdatePackage(pr.Id, false, false);
                        if(recursive)
                        {
                            var p = localrepo.FindPackage(pr.Id);
                            var pkgs = NuGetTools.ResolveDependencies(p, fn, repo);
                            logger.Indents += 2;
                            foreach(var pkg in pkgs)
                            {
                                Console.WriteLine("  * updating depending package '{0}'...", pkg.GetFullName());
                                pm.UpdatePackage(pkg.Id, false, false);
                            }
                            logger.Indents -= 2;
                        }
                    }
                }
                Console.WriteLine("* saving to {0}...", proj.FullFileName);
                proj.Save(proj.FullFileName);
            }
            catch (InvalidOperationException e)
            {
                Report.Fatal(e.Message);
            }
        }
    }
}

