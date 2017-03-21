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
    public class NuGetInstallCommand : Command
    {
        string projName;
        Option<string> slnName;
        Option<string> target;
        Option<string> config;
        string sourceUrl = "https://packages.nuget.org/api/v2";
        bool verbose = false;
        bool allowPre = false;
        bool force = false;

        public NuGetInstallCommand()
            : base("dnproj nuget install",
                   @"install NuGet packages to project.", "install NuGet packages.", "<name[:version]>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("s=|sln=", "specify solution file. the 'packages' directory will be placed at the solution directory, and the 'packages.config' file will be placed at the project directory.", s => slnName = s);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s);
            Options.Add("o=|output-dir=", "specify where the downloaded packages will be saved to. [default=./packages]", s => target = s);
            
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
            Options.Add("a|allow-prerelease", "use the pre release version of packages, if available.", _ => allowPre = _ != null);
            Options.Add("f|force", "ignore all warnings and errors.", _ => force = _ != null);

            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);

            this.AddTips("when both of --sln and --output-dir are used at the same time, the latter will be ignored.\nwhen --config is used, it will be preferred even if --sln is used too.");
            this.AddTips("when --config is not used, dnproj will try to use a 'packages.config'\nin the same directory as your project file.");
        
            this.AddExample("$ dnproj nuget install EntityFramework");
            this.AddExample("$ dnproj nuget install EntityFramework --config ../packages.config --output-dir ../packages ");
            this.AddExample("$ dnproj nuget install EntityFramework:5.0.0 --sln ../ConsoleApplication1.sln");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args, Option<string> incompleteInput = default(Option<string>))
        {
            return this.GenerateSuggestions
            (
                args,
                i =>
                {
                    switch(i)
                    {
                        case "-p":
                        case "--proj":
                            return CommandSuggestion.Files("*proj");
                        case "-s":
                        case "--sln":
                            return CommandSuggestion.Files("*sln");
                        case "-c":
                        case "--config":
                            return CommandSuggestion.Files("packages.config");
                        case "-o":
                        case "--output-dir":
                            return CommandSuggestion.Directories();
                        default:
                            return CommandSuggestion.None;
                    }
                },
                () =>
                {
                    try
                    {
                        return incompleteInput.Match(
                            i =>
                            {
                                var repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl);
                                return CommandSuggestion.Values(repo.GetCachedPackageNames().Filter(x => x.StartsWith(i)));
                            },
                            () => CommandSuggestion.None
                        );
                    }
                    catch
                    {
                        return CommandSuggestion.None;
                    }
                },
                    
                _ =>
                {
                    try
                    {
                        return incompleteInput.Match(
                            i =>
                            {
                                var repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl);
                                return CommandSuggestion.Values(repo.GetCachedPackageNames().Filter(x => x.StartsWith(i)));
                            },
                            () => CommandSuggestion.None
                        );
                    }
                    catch
                    {
                        return CommandSuggestion.None;
                    }
                }
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            try
            {
                var proj = this.LoadProject(ref args, ref projName);
                if (args.Count() > 0)
                {
                    // resolve pathes
                    var path = target.Map(Path.GetFullPath)
                    .Default(Path.Combine(Path.GetDirectoryName(slnName.Default(proj.FullFileName)), "packages"));

                    var confloc = config.Map(Path.GetFullPath)
                        .Default(
                            slnName.HasValue 
                            ? Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")
                            : Path.Combine(Directory.GetParent(path).FullName, "packages.config")
                        );

                    if(!File.Exists(confloc))
                        Report.Warning("'{0}' doesn't exist, creating.", confloc);

                    var conf = new PackageReferenceFile(confloc);

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
                    var pm = new DNPackageManager(repo, path, verbose);

                    pm.DependencyVersion = DependencyVersion.Lowest;
                    pm.SkipPackageTargetCheck = true;
                    pm.CheckDowngrade = true;
                    pm.Logger.IsSilent = !verbose;
                    pm.PackageInstalled += (sender, e) =>
                    {
                        var pn = e.Package.GetFullName().Replace(' ', '.');
                        var bp = new Uri(proj.FullFileName);
                        e.Package.AssemblyReferences
                            .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                            .Find(x => 
                                   x.SupportedFrameworks.Contains(fn) 
                                || e.AlternativeFramework.Map(x.SupportedFrameworks.Contains).Default(false)
                                || force
                            )
                            .Match(a =>
                            {
                                var tgfn =
                                    a.SupportedFrameworks.Contains(fn) 
                                    ? fn 
                                    : e.AlternativeFramework.HasValue
                                      ? e.AlternativeFramework.Value
                                      : force
                                        ? a.TargetFramework
                                        : null;

                                conf.AddEntry(e.Package.Id, e.Package.Version, false, tgfn);

                                var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                                var rp = bp.MakeRelativeUri(absp).ToString();
                                var an = Assembly.LoadFile(absp.AbsolutePath).GetName().Name;
                                if (verbose)
                                    Report.WriteLine(pm.Logger.Indents, "Adding reference to '{0}, HelpPath={1}'.", an, rp);
                                var i = proj.ReferenceItemGroup().AddNewItem("Reference", an);
                                i.SetMetadata("HintPath", rp);

                                if(!verbose && fn == tgfn)
                                    Report.WriteLine(pm.Logger.Indents - 2, "* successfully installed '{0}'.", e.Package.GetFullName());
                                else if(!verbose)
                                    Report.WriteLine(pm.Logger.Indents - 2, "* successfully installed '{0}' (TargetFramework='{1}').", e.Package.GetFullName(), tgfn);
                            },
                            () => 
                            {
                                if(!force)
                                {
                                    if(e.Package.AssemblyReferences.Any())
                                    {
                                        Report.Error(pm.Logger.Indents, "no assemblies in '{0}' supports framework '{1}', cancelling.", e.Package.GetFullName(), fn);
                                        Report.Error(pm.Logger.Indents, "supported frameworks are: {0}", e.Package.AssemblyReferences.Map(x => x.TargetFramework).JoinToString(", "));
                                        NuGetTools.FindUpperCompatibleFramework(e.Package, fn)
                                                  .Match
                                        (
                                            uf => 
                                            {
                                                Report.Info(pm.Logger.Indents, "consider upgrading target framework to '{0}'.", uf);
                                                Report.Info(pm.Logger.Indents, "> 'dnproj conf set TargetFrameworkVersion \"v{0}\"'", uf.Version);
                                            },
                                            () => Report.Info(pm.Logger.Indents, "'--force' to ignore this error, if needed.")
                                        );
                                        var lpm = new PackageManager(localrepo, path);
                                        lpm.UninstallPackage(e.Package, true);
                                        e.Cancel = true;
                                        Environment.Exit(1);
                                    }
                                    else
                                    {
                                        Report.Warning(pm.Logger.Indents, "no assemblies to install found in '{0}', ignoring.", e.Package.GetFullName());
                                        conf.AddEntry(e.Package.Id, e.Package.Version);
                                    }
                                }
                                else
                                {
                                    Report.Warning(pm.Logger.Indents, "no assemblies to install found in '{0}', ignoring.", e.Package.GetFullName());
                                    conf.AddEntry(e.Package.Id, e.Package.Version);
                                }
                            });
                    };

                    // install
                    foreach (var name in args)
                    {
                        var version = Option<string>.None;
                        var id = name;
                        if (name.Contains(":"))
                        {
                            version = name.Split(':')[1];
                            id = name.Split(':')[0];
                        }

                        var p = pm.InstallPackageWithValidation(fn, id, version, allowPre, force);

                        if(!p.HasValue)
                            Environment.Exit(1);
                        
                        p.Map(x => NuGetTools.ResolveDependencies(x, fn, repo))
                         .May(pkgs =>
                        {
                            pm.Logger.Indents += 2;
                            Console.WriteLine("* installing depending packages...");
                            if(pkgs.Map(pkg => pm.InstallPackageWithValidation(fn, pkg.Id, pkg.Version, allowPre, force)).Any(x => !x.HasValue))
                            {
                                Console.WriteLine("* removing broken installed packages...");
                                var lpm = new PackageManager(localrepo, path);

                                Console.WriteLine("* removing '{0}'...", p.Value.Id);
                                lpm.UninstallPackage(p.Value, true);
                                conf.DeleteEntry(p.Value.Id, p.Value.Version);

                                pkgs.Try(x => x.Iter(y => 
                                {
                                    Console.WriteLine("* removing '{0}'...", y.Id);
                                    lpm.UninstallPackage(y, true);
                                    conf.DeleteEntry(y.Id, y.Version);
                                }));
                                Environment.Exit(1);
                            }
                            pm.Logger.Indents -= 2;
                        });
                    }
                    Console.WriteLine("* saving to {0}...", proj.FullFileName);
                    proj.Save(proj.FullFileName);
                }
                else
                {
                    Report.Error("name(s) not specified.");
                    Console.WriteLine();
                    Help(args);
                }
            }
            catch (NullReferenceException)
            {
                throw;
            }
            catch (Exception e)
            {
                Report.Fatal(e.Message);
            }
        }
    }
}

