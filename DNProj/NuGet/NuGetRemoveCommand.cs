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
    public class NuGetRemoveCommand : Command
    {
        string projName;
        Option<string> config;
        bool verbose = false;
        bool force = false;
        bool recursive = false;

        public NuGetRemoveCommand()
            : base("dnproj nuget remove",
                   @"remove NuGet packages from project.", "remove NuGet packages.", "<name>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("f|force", "force remove packages although unresolved dependencies still exist.", _ => force = _ != null);
            Options.Add("r|recursive", "remove packages' dependencies too.", _ => recursive = _ != null);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s.Some());
            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);

            this.AddTips("when --config is not used, dnproj will try to use a 'packages.config'\nin the same directory as your project file.");
            this.AddExample("$ dnproj nuget remove EntityFramework");
        }

        public override void Run(IEnumerable<string> args)
        {
            var proj = this.LoadProject(ref args, ref projName);
            if (args.Count() > 0)
            {
                // find packages.config
                var conf = new PackageReferenceFile(config.Map(Path.GetFullPath)
                    .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")));

                if(!File.Exists(conf.FullPath))
                    Report.Fatal("'{0}' does not exist.", conf);

                // find packages/
                var path = proj.ReferenceItems()
                    .Map(ProjectTools.GetAbsoluteHintPath)
                    .FindSome()
                    .Map(x => Path.GetDirectoryName(x).Unfold(s => Tuple.Create(Directory.GetParent(s), Directory.GetParent(s).FullName)).Nth(2))
                    .Map(x => x.FullName).AbortNone(() =>
                    Report.Fatal("there is no NuGet package installed.")
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
                var repo = new LocalPackageRepository(path);
                var pm = new PackageManager(repo, path);
                var logger = new NuGetLogger(2, !verbose);
                pm.Logger = logger;
                pm.DependencyVersion = DependencyVersion.Lowest;
                pm.SkipPackageTargetCheck = true;
                pm.PackageUninstalled += (sender, e) =>
                {
                    var pn = e.Package.GetFullName().Replace(' ', '.');
                    var bp = new Uri(proj.FullFileName);
                    conf.DeleteEntry(e.Package.Id, e.Package.Version);
                    e.Package.AssemblyReferences
                        .Iter(a =>
                    {
                        var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                        var rp = bp.MakeRelativeUri(absp).ToString();
                        proj.ReferenceItems()
                            .Filter(x => x.HasMetadata("HintPath"))
                            .Find(x => x.GetMetadata("HintPath") == rp)
                            .Match(
                                proj.ReferenceItemGroup().RemoveItem,
                                () => {}
                            );
                    });
                    Report.WriteLine("* successfully uninstalled '{0}'.", e.Package.GetFullName());
                };

                // uninstall
                try
                {
                    foreach (var name in args)
                    {
                        var p = repo.FindPackage(name);

                        if (p == null)
                        {
                            Report.Error(2, "package '{0}' is not installed.", name);
                            break; 
                        }

                        logger.Indents = 2;

                        if(recursive)
                        {
                            var deps = NuGetTools.ResolveDependencies(p, fn, repo);
                            logger.Filter = logger.Filter.Overwrite(x => !x.Contains("Unable to locate dependency") || deps.Any(y => x.Contains(y.Id)));
                            Console.WriteLine("* uninstalling '{0}' and its dependencies...", p.GetFullName());
                        }
                        else
                            Console.WriteLine("* uninstalling '{0}'...", p.GetFullName());

                        pm.UninstallPackage(p, force, recursive);
                        logger.Filter = NX.Option.None;
                    }
                    Console.WriteLine("* saving to {0}...", proj.FullFileName);
                    proj.Save(proj.FullFileName);
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
            else
            {
                Report.Error("name(s) not specified.");
                Console.WriteLine();
                Help(args);
            }
        }
    }
}

