﻿/*
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
        bool recursive = false;
        bool allowPre = false;
        bool force = false;

        public NuGetUpdateCommand(string name)
            : base(name,
                            @"update NuGet packages.

if no package names are specified, it will try to) update all the installed packages.", "update NuGet packages.", "[name]*", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s);
            Options.Add("r|recursive", "update packages' dependencies too.", _ => recursive = _ != null);
            Options.Add("a|allow-prerelease", "use the pre release version of packages, if available.", _ => allowPre = _ != null);
            Options.Add("f|force", "ignore all warnings and errors.", _ => force = _ != null);
            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);

            this.AddTips("when --config is not used, dnproj will try to use a 'packages.config'\nin the same directory as your project file.");
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
                        case "-c":
                        case "--config":
                            return CommandSuggestion.Files("packages.config");
                        default:
                            return CommandSuggestion.None;
                    }
                },
                () =>
                {
                    var p = ProjectTools.GetProject(projName);
                    if (p.HasValue)
                    {
                        var proj = p.Value;
                        var confPath = config.Map(Path.GetFullPath)
                            .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config"));
                        if(File.Exists(confPath))
                        {
                            var conf = new PackageReferenceFile(confPath);
                            return CommandSuggestion.Values(conf.GetPackageReferences().Map(x => x.Id));
                        }
                        else return CommandSuggestion.None;
                    }
                    else
                        return CommandSuggestion.None;
                },
                _ =>
                {
                    var p = ProjectTools.GetProject(projName);
                    if (p.HasValue)
                    {
                        var proj = p.Value;
                        var confPath = config.Map(Path.GetFullPath)
                            .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config"));
                        if(File.Exists(confPath))
                        {
                            var conf = new PackageReferenceFile(confPath);
                            return CommandSuggestion.Values(conf.GetPackageReferences().Map(x => x.Id));
                        }
                        else return CommandSuggestion.None;
                    }
                    else
                        return CommandSuggestion.None;
                }
            );
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
                    .Choose(ProjectTools.GetAbsoluteHintPath)
                    .Head()
                    .Map(x => Path.GetDirectoryName(x).Unfold(s => Tuple.Create(Directory.GetParent(s), Directory.GetParent(s).FullName)).Nth(2))
                    .Flatten()
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
                var pm = new DNPackageManager(repo, path);
                pm.DependencyVersion = DependencyVersion.Lowest;
                pm.SkipPackageTargetCheck = true;
                pm.Logger.IsSilent = !verbose;

                pm.PackageUninstalling += (sender, e) => 
                {
                    var i = e.Package.GetFullName().Replace(' ', '.');
                    e.Package.ExtractContents(new PhysicalFileSystem(path), i + ".backup");
                    File.Copy(Path.Combine(Path.Combine(path, i), i + ".nupkg"), Path.Combine(Path.Combine(path, i + ".backup"), i + ".nupkg")); 
                };

                pm.PackageInstalling += (sender, e) =>
                {
                    var alt = e.Package.FindLowerCompatibleFramework(fn);
                    if(!force && !e.Package.GetSupportedFrameworks().Contains(fn) && !alt.HasValue)
                    {
                        Report.Error(pm.Logger.Indents, "newer version of '{0}' doesn't support framework '{1}', cancelling...", e.Package.Id, fn);
                        e.Cancel = true;
                    }
                };

                pm.PackageInstalled += (sender, e) =>
                {
                    var pn = e.Package.GetFullName().Replace(' ', '.');
                    var bp = new Uri(proj.FullFileName);
                    var alt = e.Package.FindLowerCompatibleFramework(fn);
                    e.Package.AssemblyReferences
                        .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                        .Find(x => 
                               force 
                            || x.SupportedFrameworks.Contains(fn) 
                            || alt.Map(x.SupportedFrameworks.Contains).Default(false)
                        )
                        .Match(a =>
                        {
                            conf.GetPackageReferences()
                                .Find(x => x.Id == e.Package.Id)
                                .May(old =>
                                {
                                    var back = Path.Combine(path, old.Id + "." + old.Version + ".backup");
                                    Directory.Delete(back, true);
                                    conf.DeleteEntry(old.Id, old.Version);
                                });
                            if(a.SupportedFrameworks.Contains(fn))
                                conf.AddEntry(e.Package.Id, e.Package.Version, false, fn);
                            else if(alt.HasValue)
                                conf.AddEntry(e.Package.Id, e.Package.Version, false, alt.Value);
                            else if(force)
                                conf.AddEntry(e.Package.Id, e.Package.Version, false, a.TargetFramework);
                            
                            var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                            var rp = bp.MakeRelativeUri(absp).ToString();
                            var an = Assembly.LoadFile(absp.AbsolutePath).GetName().Name;
                            proj.ReferenceItems()
                                .Filter(x => x.HasMetadata("HintPath"))
                                .Find(x => x.Include == an)
                                .Match(
                                    i =>
                                    {
                                        if (verbose)
                                            Report.WriteLine(pm.Logger.Indents, "Replacing existing reference to '{0}, HelpPath={1}'.", an, rp);
                                        proj.ReferenceItemGroup().RemoveItem(i);
                                        
                                    },
                                    () =>
                                    {
                                        if(verbose)
                                            Report.WriteLine(pm.Logger.Indents, "Adding a new reference to '{0}'.", an);    
                                    }
                                );
                            var j = proj.ReferenceItemGroup().AddNewItem("Reference", an);
                            j.SetMetadata("HintPath", rp);
                        },
                        () => 
                        {
                            var lpm = new PackageManager(localrepo, path);
                            lpm.UninstallPackage(e.Package, true);
                            conf.GetPackageReferences()
                                .Find(x => x.Id == e.Package.Id)
                                .May(old =>
                                {
                                    if(verbose)
                                        Report.WriteLine(pm.Logger.Indents, "Restoring previous version of '{0}'.", old.Id);
                                    var back = Path.Combine(path, old.Id + "." + old.Version);
                                    Directory.Move(back + ".backup", back);
                                });
                        });
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
                        pm.UpdatePackage(name, false, allowPre);

                        if (recursive)
                        {
                            var p = localrepo.FindPackage(name);
                            var pkgs = NuGetTools.ResolveDependencies(p, fn, repo);
                            pm.Logger.Indents += 2;
                            foreach (var pkg in pkgs)
                            {
                                Console.WriteLine("  * updating depending package '{0}'...", pkg.GetFullName());
                                pm.UpdatePackage(pkg.Id, false, allowPre);
                            }
                            pm.Logger.Indents -= 2;
                        }
                    }
                }
                else
                {
                    foreach (var pr in conf.GetPackageReferences())
                    {
                        Console.WriteLine("* updating '{0}'...", pr.Id);
                        pm.UpdatePackage(pr.Id, false, allowPre);
                        if (recursive)
                        {
                            var p = localrepo.FindPackage(pr.Id);
                            var pkgs = NuGetTools.ResolveDependencies(p, fn, repo);
                            pm.Logger.Indents += 2;
                            foreach (var pkg in pkgs)
                            {
                                Console.WriteLine("  * updating depending package '{0}'...", pkg.GetFullName());
                                pm.UpdatePackage(pkg.Id, false, allowPre);
                            }
                            pm.Logger.Indents -= 2;
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

