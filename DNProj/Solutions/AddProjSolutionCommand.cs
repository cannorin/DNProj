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
    public class AddProjSolutionCommand : Command
    {
        string slnName;

        public AddProjSolutionCommand()
            : base("dnsln add-proj", "add project to solution.", "add project to solution.", "<projectfile>", "[options]")
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
                },
                () => CommandSuggestion.Files("*proj*"),
                _ => CommandSuggestion.Files("*proj*")
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            var s = this.LoadSolution(ref args, ref slnName);
            
            foreach(var pf in args)
            {
                var pn = ProjectTools.GetName(pf);
                var pt = ProjectTools.GetProjectType(pf);
                var sp = new SlnProjectBlock(pn, pf, pt, Guid.NewGuid());
                s.Projects.Add(sp);
                if(sp.ProjectType.Guid == ProjectType.CSharp.Guid
                || sp.ProjectType.Guid == ProjectType.FSharp.Guid)
                    s.ApplyConfigurationPlatform(sp);
            }

            s.SaveTo(slnName);
        }

    }
}
