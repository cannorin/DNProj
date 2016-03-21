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
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class ListRefProjectCommand : Command
    {
        string projName;

        public ListRefProjectCommand()
            : base("dnproj ls-ref", "show references in speficied project.", "show references.", "[options]")
        {
            Options.Add("p=|proj=", "specify project file, not in the current directory.", p => projName = p);
        }

        public override void Run(IEnumerable<string> args)
        {
            var p = this.LoadProject(ref args, ref projName);
            foreach (var px in p.ReferenceItems())
            {
                Console.WriteLine(px.Include);
                if (!string.IsNullOrEmpty(px.Condition))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("    Condition: {0})", px.Condition);
                    Console.ResetColor();
                }
                if (px.HasMetadata("HintPath"))
                    Console.WriteLine("    HintPath: {0}", px.GetEvaluatedMetadata("HintPath"));
            }
        }
    }
}

