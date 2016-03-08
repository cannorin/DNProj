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
using Mono.Options;
using NX;

namespace DNProj
{
    public static class Tools
    {
        public static void FailWith(string s, params object[] args)
        {
            Console.WriteLine(s, args);
            Environment.Exit(1);
        }

        public static List<string> SafeParse(this OptionSet o, IEnumerable<string> args)
        {
            return o.Try(xs => xs.Parse(args)).MatchEx(x => x, e =>
            {
                e.Match(
                    xe => Tools.FailWith("error: {0}", e.Value.Message), 
                    () => Tools.FailWith("error: failed to parse arguments.")
                );
                return null;
            });
        }
    }
}

