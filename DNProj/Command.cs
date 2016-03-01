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
using System.Threading;
using Mono.Options;
using NX;

namespace DNProj
{
    public abstract class Command
    {
        public Dictionary<string, Action<IEnumerable<string>>> Commands;
        public OptionSet Options;

        public Command()
        {
            Options = new OptionSet();
            Commands = new Dictionary<string, Action<IEnumerable<string>>>();
            Commands["help"] = Help;
            Commands["--help"] = Help;
            Commands["-h"] = Help;
        }

        public abstract void Help(IEnumerable<string> args);

        public void Run(IEnumerable<string> args)
        {
            var rs = Options.Parse(args);
            if (rs.Count == 0)
                Help(rs);
            else
            {
                var c = rs.Hd();

                if (Commands.ContainsKey(c))
                    Commands[c](rs.Skip(1));
                else if (Commands.ContainsKey("this"))
                    Commands["this"](rs);
                else
                {
                    Console.WriteLine("command {0} not found.", c);
                    Help(rs);
                }
            }
        }
    }
}
