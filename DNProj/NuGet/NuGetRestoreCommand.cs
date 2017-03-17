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
    public class NuGetRestoreCommand : Command
    {
        string projName;
        Option<string> config;
        string sourceUrl = "https://packages.nuget.org/api/v2";
        bool verbose = false;

        public NuGetRestoreCommand(string name)
            : base(name,
                @"restore NuGet packages.", "restore NuGet packages.", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s.Some());
            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);

            this.AddTips("when --config is not used, dnproj will try to use a 'packages.config'\nin the same directory as your project file.");
        }

        public override void Run(IEnumerable<string> args)
        {
            try
            {
                var proj = this.LoadProject(ref args, ref projName);

                // resolve pathes

                var conf = new PackageReferenceFile(config.Map(Path.GetFullPath)
                    .Default(Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")));

                if(!File.Exists(conf.FullPath))
                    Report.Fatal("'{0}' does not exist.", conf);

                var prs = conf.GetPackageReferences();
                var path = proj.ReferenceItems()
                    .Choose(ProjectTools.GetAbsoluteHintPath)
                    .Filter(x => prs.Any(pr => x.Contains(pr.Id)))
                    .Map(x => Path.GetDirectoryName(x).Unfold(s => Tuple.Create(Directory.GetParent(s), Directory.GetParent(s).FullName)).Nth(2))
                    .Map(x => x.FullName)
                    .Head()
                    .AbortNone(() =>
                    {
                        Report.Fatal("there are no referenced NuGet packages.");
                    });

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

                pm.PackageInstalled += (sender, e) =>
                {
                    var pn = e.Package.GetFullName().Replace(' ', '.');
                    var bp = new Uri(proj.FullFileName);
                    var pr = conf.GetPackageReferences().Find(x => x.Id == e.Package.Id && x.Version == e.Package.Version);
                    e.Package.AssemblyReferences
                        .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                        .Find(x => pr.HasValue && pr.Value.TargetFramework == x.TargetFramework)
                        .May(a =>
                        {
                            var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                            var rp = bp.MakeRelativeUri(absp).ToString();
                            var an = Assembly.LoadFile(absp.AbsolutePath).GetName().Name;

                            proj.ReferenceItems()
                                .Filter(x => x.HasMetadata("HintPath"))
                                .Find(x => x.Include == an)
                                .Match(i =>
                                {
                                    if(verbose)
                                        Report.WriteLine(pm.Logger.Indents, "Validating existing reference to the package '{0}'...", an);
                                    var hp = i.GetMetadata("HintPath");
                                    if(hp != rp)
                                    {
                                        Report.Warning("overwriting the existing reference to '{0}' with '{1}'...", hp, rp);
                                        i.SetMetadata("HintPath", rp);
                                    }
                                },
                                () => 
                                {
                                    Report.Warning("new package '{0}' has been added.", an);
                                    if(verbose)
                                        Report.WriteLine(pm.Logger.Indents, "Adding reference to '{0}, HelpPath={1}'...", an, rp);
                                    var i = proj.ReferenceItemGroup().AddNewItem("Reference", an);
                                    i.SetMetadata("HintPath", rp);
                                });
                        });
                };

                // restore
                foreach(var pr in prs)
                {
                    if(!localrepo.Exists(pr.Id, pr.Version))
                    {
                        Console.WriteLine("* restoring '{0}'...", pr.Id);
                        pm.InstallPackageWithValidation(fn, pr.Id, pr.Version.Some(), true, true);
                    }
                    else
                        Report.Warning("'{0}' is already installed, skipping...", pr.Id);
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

