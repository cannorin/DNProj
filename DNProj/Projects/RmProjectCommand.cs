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

namespace DNProj
{
    public class RmProjectCommand : Command
    {
        string projName;

        public RmProjectCommand()
            : base("dnproj rm", "remove files from specified project.", "remove files.", "<filename>+", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
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
                        default:
                            return CommandSuggestion.None;
                    }
                },
                () =>
                {
                    var p = ProjectTools.GetProject(projName);
                    if (p.HasValue)
                    {
                        var gs = p.Value.SourceItemGroup().Cast<BuildItem>().Map(x => x.Include);
                        if(gs.Any())
                            return CommandSuggestion.Values(gs);
                        else
                            return CommandSuggestion.None;
                    }
                    else
                        return CommandSuggestion.None;
                },
                _ =>
                {
                    var p = ProjectTools.GetProject(projName);
                    if (p.HasValue)
                    {
                        var gs = p.Value.SourceItemGroup().Cast<BuildItem>().Map(x => x.Include);
                        if(gs.Any())
                            return CommandSuggestion.Values(gs);
                        else
                            return CommandSuggestion.None;
                    }
                    else
                        return CommandSuggestion.None;
                }
            );
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var g = p.SourceItemGroup();
            if(!args.Any())
            {
                Report.Error("file(s) not specified.");
                Console.WriteLine();
                Help(args);
                return;
            }
            foreach (var s in args)
                g.Cast<BuildItem>()
                    .Try(xs => xs.First(x => x.Include == s))
                    .Match(
                    g.RemoveItem, 
                    () => Report.Fatal("item with name '{0}' doesn't exist.", s)
                );
            p.Save(p.FullFileName);
        }
    }
}

