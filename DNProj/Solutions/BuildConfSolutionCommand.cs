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
    public class BuildConfSolutionCommand : Command
    {
        string slnName;

        public BuildConfSolutionCommand()
            : base("dnsln buildconf", "manage build configurations in the specified solution.", "manage build configurations.", "<subcommand>", "[options]")
        {
            Options.Add("s=|solution=", "specify solution file.", s => slnName = s);

            Commands["list"] = Child(
                args => {
                    var s = Commands["list"].LoadSolution(ref args, ref slnName);
                    s.GetConfigurationPlatforms().Iter(Console.WriteLine);
                }, "list", "list configurations.", "list configurations.", "[options]");
            
            Commands["add"] = Child(
                args => {
                    var s = Commands["add"].LoadSolution(ref args, ref slnName);
                    if(!args.Any())
                        Tools.FailWith("argument is empty");
                    else if(args.Count() > 1)
                        Tools.FailWith("you can add only one configuration at once.");
                    var c = args.First();
                    s.AddConfigurationPlatform(c);
                    s.SaveTo(s.FileName.Value);
                }, "add", "add configuration.", "add configuration.", "<build-configuration>", "[options]");

            Commands["remove"] = Child(
                args => {
                    var s = Commands["remove"].LoadSolution(ref args, ref slnName);
                    var cs = s.GetConfigurationPlatforms();
                    
                    foreach(var c in args)
                    {
                        if(cs.Contains(c))
                            s.RemoveConfigurationPlatform(c);
                        else
                            Report.Warning("the configuration '{0}' doesn't exist.", c);
                    }
                    s.SaveTo(s.FileName.Value);
                }, "remove", "remove configurations.", "remove configurations.", "<build-configuration>+", "[options]");

            /*
            Commands["apply"] = Child(
                args => {
                    var s = Commands["apply"].LoadSolution(ref args, ref slnName);
                    var pn = args.Head();
                    if(!pn.HasValue)
                        Report.Fatal("argument is empty.");
                    else if(!s.Projects.Any(pn.Value.Equals))
                        Report.Fatal("the project '{0}' doesn't exist");
                    else
                    {
                        var p = s.Projects.First(pn.Value.Equals);
                        var cn = args.Skip(1).Head();
                        
                        if(!cn.HasValue)
                            Report.Fatal("configuration name is empty.");
                        else if(cn.Value == "all")
                        {
                            Report.Warning("applying all the build platforms to '{0}'...", pn.Value);
                            s.ApplyConfigurationPlatform(p);
                        }
                        else if(!s.GetConfigurationPlatforms().Any(cn.Value.Equals))
                            Report.Fatal("the configuration '{0}' doesn't exist.", cn.Value);
                        else
                            foreach(var c in args.Skip(1))
                                s.AddConfigurationToProject(c, p);
                        s.SaveTo(s.FileName.Value);
                    }
                }, "apply", "apply configuration to project.", "apply configuration to project.", "<projectname>", "(all | <build-configuration>)", "[options]");
            */
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
                incompleteInput: incompleteInput
            );
        }

        public override IEnumerable<CommandSuggestion> GetChildSuggestions(ChildCommand child, IEnumerable<string> args, Option<string> incompleteOption = default(Option<string>))
        {
            switch(child.Name.Split(' ').Last())
            {
                case "remove":
                    var s = SolutionTools.GetSolution(slnName);
                    return CommandSuggestion.Values(s.Map(x => x.GetConfigurationPlatforms()).Default(New.Array<string>())).Singleton();
                case "add":
                case "list":
                default:
                    return CommandSuggestion.None.Singleton();
            }
        } 
    }
}
