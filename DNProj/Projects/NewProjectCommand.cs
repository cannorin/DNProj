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
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class NewProjectCommand : Command
    {
        string outputdir = "";
        string temp = "none";
        string type = "exe";
        string arch = "AnyCPU";
        string fw = "v4.5";

        public NewProjectCommand()
            : base("dnproj new", 
                   "create new project.", 
                   "create new project.", "<filename>", "[options]")
        {
            Options.Add("d=|output-dir=", "specify output directory. \n[default=<current directory>]", x => outputdir = x);
            Options.Add("t=|template=", "specify language template. \n(none|csharp|fsharp) [default=none]", x => temp = x);
            Options.Add("o=|output-type=", "specify output type. \n(library|exe|winexe|module) [default=exe]", x => type = x);
            Options.Add("p=|platform=", "specify platform target. \n(AnyCPU|x86|x64) [default=AnyCPU]", x => arch = x);
            Options.Add("f=|target-framework=", "specify target framework. \n[default=v4.5]", x => fw = x);

            this.AddExample("$ dnproj new ConsoleApplication1.csproj -d ConsoleApplication1 -t csharp");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args, Option<string> incompleteInput = default(Option<string>))
        {
            return this.GenerateSuggestions(args,
                i =>
                {
                    switch(i)
                    {
                        case "-d":
                        case "--output-dir":
                            return CommandSuggestion.Directories();
                        case "-t":
                        case "--template":
                            return CommandSuggestion.Values("none", "csharp", "fsharp");
                        case "-o":
                        case "--output-type":
                            return CommandSuggestion.Values("library", "exe", "winexe", "module");
                        case "-p":
                        case "--platform":
                            return CommandSuggestion.Values("AnyCPU", "x86", "x64");
                        case "-f":
                        case "--target-framework":
                            return CommandSuggestion.Values("v2.0", "v3.0", "v3.5", "v4.0", "v4.5", "v4.5.1", "v4.5.2", "v4.6"); 
                        default:
                            return CommandSuggestion.None;
                    }
                }
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            args = Options.SafeParse(args);
            if (!args.Any() || args.Any(Templates.HelpOptions.Contains))
            {
                Help(args);
                return;
            }
            if (!(temp == "csharp" || temp == "fsharp" || temp == "none"))
                Report.Fatal("invalid language template '{0}'.", temp);
            if (!new []{ "library", "exe", "winexe", "module" }.Contains(type))
                Report.Fatal("invalid output type '{0}'.", type);

            var fn = args.First();
            if (!Path.GetFileName(fn).EndsWith("proj"))
                fn += ".proj";
            var f = Templates.GenPath(outputdir, fn);
            var name = Path.GetFileName(fn).Split('.').Rev().Skip(1).Rev().JoinToString(".");

            if (!string.IsNullOrEmpty(outputdir) && !Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);
            if (!File.Exists(f))
                File.Create(f).Close();

            // prepare project
            var p = new Project();
            p.DefaultTargets = "Build";
            p.DefaultToolsVersion = "4.0";

            // add assembly properties
            var assemblyProperty = p.AddNewPropertyGroup(false);
            var defcond = assemblyProperty.AddNewProperty("Configuration", "Debug");
            defcond.Condition = " '$(Configuration)' == '' ";
            var defarch = assemblyProperty.AddNewProperty("Platform", arch);
            defarch.Condition = " '$(Platform)' == '' ";
            assemblyProperty.AddNewProperty("ProjectGuid", "{" + Guid.NewGuid().ToString().ToUpper() + "}");
            assemblyProperty.AddNewProperty("OutputType", type.Substring(0, 1).ToUpper() + type.Substring(1));
            assemblyProperty.AddNewProperty("RootNamespace", name);
            assemblyProperty.AddNewProperty("AssemblyName", name);
            assemblyProperty.AddNewProperty("TargetFrameworkVersion", fw);

            // add properties for each configurations
            var gs = ProjectTools.AddDefaultConfigurations(p, arch);
            var debugProperty = gs.Item1;
            var releaseProperty = gs.Item2;

            // add sources
            var sourceItems = p.AddNewItemGroup();

            // add references
            var referenceItems = p.AddNewItemGroup();
            referenceItems.AddNewItem("Reference", "System");
            if (temp == "fsharp")
            {
                referenceItems.AddNewItem("Reference", "mscorlib");
                referenceItems.AddNewItem("Reference", "FSharp.Core");
                referenceItems.AddNewItem("Reference", "System.Core");
                referenceItems.AddNewItem("Reference", "System.Numerics");
            }

            // custom
            if (temp == "csharp")
            {
                var ai = Templates.GenPath(outputdir, Templates.AssemblyInfo);
                File.WriteAllText(ai, Templates.GenerateAssemblyInfo(name));
                sourceItems.AddNewItem("Compile", Templates.AssemblyInfo);
                if (type.Contains("exe"))
                {
                    var prg = Templates.GenPath(outputdir, "Program.cs");
                    File.WriteAllText(prg, Templates.GenerateExeClass(name));
                    sourceItems.AddNewItem("Compile", "Program.cs");
                    debugProperty.AddNewProperty("PlatformTarget", arch);
                    releaseProperty.AddNewProperty("PlatformTarget", arch);
                }
                else if (type == "library")
                {
                    var lib = Templates.GenPath(outputdir, "MyClass.cs");
                    File.WriteAllText(lib, Templates.GenerateLibraryClass(name));
                    sourceItems.AddNewItem("Compile", "MyClass.cs");
                }
            }
            else if (temp == "fsharp")
            {
                debugProperty.AddNewProperty("Tailcalls", "false");
                releaseProperty.AddNewProperty("Tailcalls", "true");

                var ai = Templates.GenPath(outputdir, Templates.AssemblyInfoFSharp);
                File.WriteAllText(ai, Templates.GenerateAssemblyInfoFSharp(name));
                sourceItems.AddNewItem("Compile", Templates.AssemblyInfoFSharp);
                if (type.Contains("exe"))
                {
                    var prg = Templates.GenPath(outputdir, "Program.fs");
                    File.WriteAllText(prg, Templates.GenerateExeFSharp());
                    sourceItems.AddNewItem("Compile", "Program.fs");
                    debugProperty.AddNewProperty("PlatformTarget", arch);
                    releaseProperty.AddNewProperty("PlatformTarget", arch);
                }
                else if (type == "library")
                {
                    var lib = Templates.GenPath(outputdir, "Component1.fs");
                    File.WriteAllText(lib, Templates.GenerateLibraryFSharp(name));
                    sourceItems.AddNewItem("Compile", "Component1.fs");
                }
            }

            // add imports
            if (temp == "csharp")
                p.AddNewImport("$(MSBuildBinPath)\\Microsoft.CSharp.targets", "");
            else if (temp == "fsharp")
                p.AddNewImport("$(MSBuildExtensionsPath32)\\..\\Microsoft SDKs\\F#\\3.1\\Framework\\v4.0\\Microsoft.FSharp.Targets", "");

            // save
            Using.SelectMany(
                File.OpenWrite(f).Use(),
                fs => new StreamWriter(fs).Use(),
                (_, sw) => p.Save(sw)
            );
        }
    }
}

