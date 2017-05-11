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

namespace DNProj
{
    public class NewSolutionCommand : Command
    {
        string outputdir;
        string temp = "none";
        string type = "exe";
        string arch = "AnyCPU";
        string fw = "v4.5";
        bool empty = false;
    
        public NewSolutionCommand()
            : base("dnsln new", "create new solution.", "create new solution.", "<filename>", "[options]")
        {

            Options.Add("d=|project-dir=", "specify project directory. \n[default=<current directory>/<project name>]", x => outputdir = x);
            Options.Add("t=|template=", "specify language template. \n(none|csharp|fsharp) [default=none]", x => temp = x);
            Options.Add("o=|output-type=", "specify output type. \n(library|exe|winexe|module) [default=exe]", x => type = x);
            Options.Add("p=|platform=", "specify platform target. \n(AnyCPU|x86|x64) [default=AnyCPU]", x => arch = x);
            Options.Add("f=|target-framework=", "specify target framework. \n[default=v4.5]", x => fw = x);
            Options.Add("e|empty", "create empty solution, ignoring all the other options.", x => empty = x != null);
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

            var fn = args.First();
            if (!Path.GetFileName(fn).EndsWith(".sln"))
                fn += ".sln";
            var f = Templates.GenPath(outputdir, fn);

            if (!string.IsNullOrEmpty(outputdir) && !Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);
            if (!File.Exists(f))
                File.Create(f).Close();

        }
    }
}

