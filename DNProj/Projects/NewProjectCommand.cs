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
using System.CodeDom;

namespace DNProj
{
    public class NewProjectCommand : Command
    {
        string outputdir = "";
        string temp = "none";
        string type = "exe";
        string arch = "AnyCPU";

        public NewProjectCommand()
            : base()
        {
            Commands["this"] = CreateProj;
            Options.Add("d=|output-dir=", "specify output directory. \n[default=<current directory>]", x => outputdir = x);
            Options.Add("t=|template=", "specify language template. \n(none|csharp|fsharp) [default=none]", x => temp = x);
            Options.Add("o=|output-type=", "specify output type. \n(library|exe|winexe|module) [default=exe]", x => type = x);
            Options.Add("p=|platform=", "specify platform target. \n(AnyCPU|x86|x64) [default=AnyCPU]", x => arch = x);
        }

        public override void Help(IEnumerable<string> _)
        {
            Console.WriteLine(
                @"usage: dnproj new <name> [options]
                
creates new project.
type 'dnproj new help' to show this.

example:
      $ dnproj new ConsoleApplication1.csproj -d ConsoleApplication1 -t csharp

options:");
            Options.WriteOptionDescriptions(Console.Out);
        }

        public void CreateProj(IEnumerable<string> args)
        {
            if (args.Count() < 1)
            {
                Console.WriteLine("error: specify filename.");
                return;
            }
            if (!(temp == "csharp" || temp == "fsharp" || temp == "none"))
            {
                Console.WriteLine("error: invalid language template '{0}'.", temp);
                return;
            }
            if (!new []{ "library", "exe", "winexe", "module" }.Contains(type))
            {
                Console.WriteLine("error: invalid output type '{0}'.", type);
                return;
            }

            var f = Templates.GenPath(outputdir, args.First());
            var name = args.First().Split('.').Rev().Skip(1).Rev().JoinToString(".");

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

            // add properties for each configurations
            var gs = ProjectTools.AddDefaultConfigurations(p, arch);
            var debugProperty = gs.Item1;
            var releaseProperty = gs.Item2;

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

            // add sources
            var sourceItems = p.AddNewItemGroup();

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
            IDisposableNX.SelectMany(
                File.OpenWrite(f).Use(),
                fs => new StreamWriter(fs).Use(),
                (_, sw) => p.Save(sw)
            );
        }
    }
}

