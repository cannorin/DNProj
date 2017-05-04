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
using NX;
using System.Linq;
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class AddRefProjectCommand : Command
    {
        string projName;
        Option<string> cond;
        Option<string> hint;

        public AddRefProjectCommand()
            : base("dnproj add-ref", 
                   @"add references to specified project.
you can add multiple references at once, when neither --cond nor --hint is used.", 
                   "add references.", "<reference-name>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
            Options.Add("c=|cond=", "specify condition.", c => cond = c);
            Options.Add("hint=", "specify hint path.", h => hint = h);

            this.AddExample("$ dnproj add-ref System.Numerics System.Xml.Linq");
            this.AddExample("$ dnproj add-ref System.Net.Http --cond \" '\\$(Platform)' == 'AnyCPU' \"");
            this.AddExample("$ dnproj add-ref System.Net.Http --cond \" '\\$(Platform)' == 'AnyCPU' \"");
            this.AddExample("$ dnproj add-ref System.Core --hint \"../packages/System.Core.1.0.0/lib/net45/System.Core.dll\"");

            this.AddWarning("you must escape the dollar sign '$' inside \"\" as \"\\$\", or use '' instead.");
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args, Option<string> incompleteInput = default(Option<string>))
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
                        case "-c":
                        case "--cond":
                            return CommandSuggestion.None;
                        case "--hint":
                            return CommandSuggestion.Files("*.dll");
                        default:
                            return CommandSuggestion.None;
                    }
                },
                () => CommandSuggestion.Values(
                    Shell.Eval("gacutil", "-l")
                        .Split('\n')
                        .Skip(1).Rev().Skip(2).Rev()
                        .Filter(x => !x.StartsWith("policy."))
                        .Choose(x => x.Try(_x => _x.Split(true, ", ")[0]))
                ),
                xs => 
                {
                    if ((cond || hint).HasValue)
                        return CommandSuggestion.None;
                    else
                        return CommandSuggestion.Values(
                            Shell.Eval("gacutil", "-l")
                                 .Split('\n')
                                 .Skip(1).Rev().Skip(2).Rev()
                                 .Filter(x => !x.StartsWith("policy."))
                                 .Choose(x => x.Try(_x => _x.Split(true, ", ")[0]))
                        );
                }
            ); 
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var g = p.ReferenceItemGroup();
            if (!args.Any())
            {
                Report.Error("reference not specified.");
                Console.WriteLine();
                Help(args);
                return;
            }
            else if (args.Count() > 1)
            {
                if ((cond || hint).HasValue)
                {
                    Report.Error("you can't add multiple references at once with --cond and/or --hint option used.");
                    Console.WriteLine();
                    Help(args);
                    return; 
                }
                else
                {
                    foreach (var name in args)
                    {
                        g.AddNewItem("Reference", name);
                    }
                }
            }
            else
            {
                var name = args.First();
                var i = g.AddNewItem("Reference", name);
                cond.May(x => i.Condition = x);
                hint.May(x => i.SetMetadata("HintPath", x));
            }
            p.Save(p.FullFileName);
        }
    }
}

