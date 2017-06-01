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

namespace DNProj
{
    public class ListSolutionCommand : Command
    {
        string slnName;

        public ListSolutionCommand()
            : base("dnsln ls", "show projects in the specified solution.", "show projects.", "[options]")
        {
            Options.Add("s=|solution=", "specify solution file.", s => slnName = s);
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args, Option<string> incompleteInput = default(Option<string>))
        {
            return this.GenerateSuggestions(args,
                i =>
                {
                    switch(i)
                    {
                        case "-s":
                        case "--solution":
                            return CommandSuggestion.Files("*.sln");
                        default:
                            return CommandSuggestion.None;
                    }
                }
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            var s = this.LoadSolution(ref args, ref slnName);
 
            foreach(var p in s.Projects)
            {
                Console.WriteLine(p.Name);
                Console.WriteLine("  guid: {0}", p.Guid.AsSlnStyle());
                Console.WriteLine("  path: {0}", p.Path);
                
                var pcp = s.GetProjectConfigurationPlatforms(p);
                if(pcp.Any())
                    Console.WriteLine("  build configurations:");
                foreach(var c in pcp)
                {
                    Console.WriteLine("    * {0}", c.Key);
                    var ac = c.Value.Find(x => x.Key.EndsWith("ActiveCfg"));
                    ac.May(x => Console.WriteLine("      - project build config: {0}", c.Key, x.Value));
                    var b = c.Value.Find(x => x.Key.EndsWith("Build.0"));
                    Console.WriteLine("      - build: {0}", b.HasValue ? "enabled" : "disabled");
                }
                Console.WriteLine();
            }
        }
    }
}
