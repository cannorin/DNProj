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
using System.Linq;
using NX;
using Microsoft.Build.BuildEngine;
using System.IO;

namespace DNProj
{
    public class AddProjectCommand : Command
    {
        string projName;
        Option<string> temp;
        bool force = false;

        public AddProjectCommand()
            : base("dnproj add", 
                   @"add existing files to specified project.
use <filename:buildaction> to specify build action.

build actions:
  Compile                    compile this file. (default)
  EmbeddedResource           embed this as resource.
  None                       do nothing.", 
                   "add files.", "<filename[:buildaction]>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
            Options.Add("f|force", "force overwrite build action.", _ => force = _ != null, true);
            Options.Add("t=|template=", "create a new file from template. you can add only one file when this option is used.\n(csharp|fsharp)", x => temp = x);

            this.AddTips("if you have a source file with ':' in its name, use ':Compile'.\n$ dnproj add Test:None.cs:Compile");
            this.AddExample("$ dnproj add A.cs B.png:EmbeddedResource C.txt:None");
            this.AddExample("$ dnproj add NewFile.cs --template csharp");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args)
        {
            return this.GenerateSuggestions(
                args,
                i =>
                {
                    switch (i)
                    {
                        case "-p":
                        case "--proj":
                            return CommandSuggestion.Files("*proj");
                        case "-t":
                        case "--template":
                            return CommandSuggestion.Values("csharp", "fsharp");
                        default:
                            return CommandSuggestion.None;
                    }
                },
                () => 
                {
                    if(!args.Contains("-t") && !args.Contains("--template"))
                        return CommandSuggestion.Files();
                    else return CommandSuggestion.None;
                },
                __ => 
                {
                    if(!args.Contains("-t") && !args.Contains("--template"))
                        return CommandSuggestion.Files();
                    else return CommandSuggestion.None;
                }
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            if (!args.Any())
            {
                Report.Error("file(s) not specified.");
                Console.WriteLine();
                Help(args);
                return;
            }
            foreach (var f in args)
            {
                var fn = f;
                var act = "Compile";
                foreach (var x in New.Seq("Compile", "EmbeddedResource", "None"))
                    if (f.EndsWith(":" + x))
                    {
                        act = x;
                        fn = f.Replace(":" + x, "");
                        break;
                    }
                    else if (x == "None" && f.Contains(":") && !f.EndsWith(":"))
                    {
                        act = f.Split(':').Last();
                        fn = f.Replace(":" + act, "");
                        if (force)
                            break;
                        else
                        {
                            Report.Error("invalid action '{0}', skipping '{1}'.", act, fn);
                            Report.Info("'--force' to use '{0}' as a build action, if needed.", act);
                            goto skip;
                        }
                    }
                
                if(temp.HasValue)
                {
                    if (args.Count() > 1)
                    {
                        Report.Error("you can't add multiple files at once with --template option used.");
                        Console.WriteLine();
                        Help(args);
                        Environment.Exit(1);
                    }
                    else
                    {
                        var t = temp.Value;
                        var dns = p.AssemblyPropertyGroup()
                                   .Cast<BuildProperty>()
                                   .Find(x => x.Name == "RootNamespace")
                                   .Map(x => x.Value)
                                   .DefaultLazy(() => Path.GetFileNameWithoutExtension(p.FullFileName));
                        var cn = Path.GetFileNameWithoutExtension(fn);
                        switch(t)
                        {
                            case "csharp":
                                File.WriteAllText(fn, Templates.GenerateLibraryClass(dns, cn));
                                break;
                            case "fsharp":
                                File.WriteAllText(fn, Templates.GenerateLibraryFSharp(dns, cn));
                                break;
                            default:
                                Report.Error("invalid template '{0}'", t);
                                Console.WriteLine();
                                Help(args);
                                Environment.Exit(1);
                                break;
                        }
                    }
                }

                p.SourceItemGroup().AddNewItem(act, fn);
                skip:
                break;
            }
            p.Save(p.FullFileName);
        }
    }
}

