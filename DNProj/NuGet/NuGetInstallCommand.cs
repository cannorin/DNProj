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
        bool allow40 = false;

        public NuGetInstallCommand()
            : base("dnproj nuget install",
                   @"install NuGet packages to project.

examples:
  $ dnproj nuget install EntityFramework --output-dir ../packages 
  $ dnproj nuget install EntityFramework:5.0.0 --sln ../ConsoleApplication1.sln

hint:
  when both of --sln and --output-dir are used at the same time, the latter will be ignored.
  when --config is used, its 'packages.config' will be preferred even if --sln is used too.

", "install NuGet packages.", "<name[:version]>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", s => projName = s);
            Options.Add("s=|sln=", "specify solution file. the 'packages' directory will be placed at the solution directory, and the 'packages.config' file will be placed at the project directory.", s => slnName = s.Some());
            Options.Add("d=|output-dir=", "specify where the downloaded packages will be saved to.[default=./packages]", s => target = s.Some());
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
            Options.Add("c=|config=", "specify 'packages.config' manually.", s => config = s.Some());
            Options.Add("a|allow-downgrade-framework", "try installing .NET 4.0 version if the package doesn't support .NET 4.5.", _ => allow40 = _ != null);
            Options.Add("v|verbose", "show detailed log.", _ => verbose = _ != null);
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
                    var conf = new PackageReferenceFile(
                               config.Map(Path.GetFullPath)
                          .Default(
                                   slnName.HasValue 
                                ? Path.Combine(Path.GetDirectoryName(proj.FullFileName), "packages.config")
                                : Path.Combine(path, "../packages.config")
                               ));
                    Directory.CreateDirectory(path);

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
                    var pm = new PackageManager(repo, path);
                    var logger = new NuGetLogger(2, !verbose);
                    pm.Logger = logger;
                    pm.DependencyVersion = DependencyVersion.Lowest;
                    pm.SkipPackageTargetCheck = true;
                    pm.CheckDowngrade = true;
                    pm.PackageInstalled += (sender, e) =>
                    {
                        var pn = e.Package.GetFullName().Replace(' ', '.');
                        var bp = new Uri(proj.FullFileName);
                        conf.AddEntry(e.Package.Id, e.Package.Version, false, fn);
                        e.Package.AssemblyReferences
                            .OrderByDescending(x => x.TargetFramework.Version.Major * 1000 + x.TargetFramework.Version.Minor)
                            .Find(x => x.SupportedFrameworks.Contains(fn) || (allow40 && x.TargetFramework.Identifier == fn.Identifier && x.TargetFramework.Version.Major == fn.Version.Major))
                            .Match(
                                a =>
                                {
                                    var absp = new Uri(Path.Combine(path, Path.Combine(pn, a.Path)));
                                    var rp = bp.MakeRelativeUri(absp).ToString();
                                    var an = Assembly.LoadFile(absp.AbsolutePath).GetName().Name;
                                    if(verbose)
                                        Report.WriteLine(logger.Indents, "Adding reference to '{0}, HelpPath={1}'...", an, rp);
                                    var i = proj.ReferenceItemGroup().AddNewItem("Reference", an);
                                    i.SetMetadata("HintPath", rp);
                                },
                                () => Report.Warning(logger.Indents, "no assemblies to install found in '{0}'.", e)
                            );
                    };

                    // install
                    foreach (var name in args)
                    {
                        var version = Option<string>.None;
                        var id = name;
                        if (name.Contains(":"))
                        {
                            version = name.Split(':')[1].Some();
                            id = name.Split(':')[0];
                        }

                        Console.WriteLine("* resolving '{0}'...", name);

                        var p = version.Match(
                                v => repo.FindPackage(id, SemanticVersion.Parse(v)),
                                () => repo.FindPackage(id)
                            );

                        var sfs = p.GetSupportedFrameworks();

                        if (p == null)
                        {
                            Report.Error(2, "package '{0}' doesn't exists.", name);
                            break; 
                        }
                        else if (!sfs.Contains(fn) 
                              && sfs.Any(x => 
                                x.Identifier == fn.Identifier && x.Version.Major == fn.Version.Major
                            ))
                        {
                            var alt = sfs.First(x => 
                                x.Identifier == fn.Identifier && x.Version.Major == fn.Version.Major
                            );
                            if(allow40)
                            {
                                Report.Warning(2, "package '{0}' doesn't support '{1}', installing '{2}' instead...", name, fn, alt);
                                fn = alt;
                            }
                            else
                            {
                                Report.Error(2, "package '{0}' doesn't support framework '{1}'.", name, fn.FullName);
                                Report.Info(2, "'--allow-downgrade-framework' to install '{0}' version instead.", alt);
                                break;
                            }
                        }
                        else if (!sfs.Contains(fn))
                        {
                            Report.Error(2, "package '{0}' doesn't support framework '{1}'.", name, fn.FullName);
                            Report.Error(2, "available frameworks are: {0}", sfs.Map(x => string.Format("'{0}'", x)).JoinToString(", "));
                            break;
                        }

                        var pkgs = NuGetTools.ResolveDependencies(p, fn, repo);

                        Console.WriteLine("* installing '{0}'...", p.GetFullName());
                        logger.Indents = 2;
                        pm.InstallPackage(p, true, true);

                        foreach (var pkg in pkgs)
                        {
                            Console.WriteLine("  * installing depending package '{0}'...", pkg.GetFullName());
                            logger.Indents = 4;
                            pm.InstallPackage(pkg, true, true);
                        }
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

