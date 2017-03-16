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

example:
  $ dnproj add-ref System.Numerics
  $ dnproj add-ref System.Xml --cond "" '\$(Platform)' == 'AnyCPU' ""
  $ dnproj add-ref System.Core --hint ""../packages/System.Core.1.0.0/lib/net45/System.Core.dll""

warning:
  on some shells such as bash, you must escape '$' charactors inside """" as ""\$"", or use '' instead.", 
                   "add references.", "<referencename>", "[options]")
        {
            Options.Add("p=|proj=", "specify project file explicitly.", p => projName = p);
            Options.Add("c=|cond=", "specify condition.", c => cond = c.Some());
            Options.Add("hint=", "specify hint path.", h => hint = h.Some());
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            var g = p.ReferenceItemGroup();
            if (!args.Any())
                Report.Fatal("missing parameter.");
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

