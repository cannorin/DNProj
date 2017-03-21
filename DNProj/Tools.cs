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
using Mono.Options;
using NX;
using System.Reflection;

namespace DNProj
{
    public static class Report
    {
        public static void Error(string s, params object[] args)
        {
            ConsoleNX.ColoredWriteLine("error: " + s, ConsoleColor.Red, args);
        }

        public static void Error(int indent, string s, params object[] args)
        {
            Seq.Repeat(' ', indent).Iter(Console.Write);
            Error(s, args);
        }

        public static void Warning(string s, params object[] args)
        {
            ConsoleNX.ColoredWriteLine("warning: " + s, ConsoleColor.Yellow, args);
        }

        public static void Warning(int indent, string s, params object[] args)
        {
            Seq.Repeat(' ', indent).Iter(Console.Write);
            Warning(s, args);
        }

        public static void Info(string s, params object[] args)
        {
            ConsoleNX.ColoredWriteLine("info: " + s, ConsoleColor.Blue, args);
        }

        public static void Info(int indent, string s, params object[] args)
        {
            Seq.Repeat(' ', indent).Iter(Console.Write);
            Info(s, args);
        }

        public static void WriteLine(string s, params object[] args)
        {
            Console.WriteLine(s, args);
        }

        public static void WriteLine(int indent, string s, params object[] args)
        {
            Seq.Repeat(' ', indent).Iter(Console.Write);
            Report.WriteLine(s, args);
        }

        public static void Fatal(string s, params object[] args)
        {
            ConsoleNX.ColoredWriteLine("fatal: " + s, ConsoleColor.Red, args);
            Environment.Exit(1);
        }


    }

    public static class Tools
    {
        public static void CallAsMain<T>(string[] args)
            where T : Command, new()
        {
            if(args.Length > 0 && args[0] == "--generate-suggestions")
            {
                var ss = new T().GetSuggestions(args.Skip(1)).Reduce();
                foreach(var s in ss)
                {
                    Console.WriteLine(string.Join(" ", s.RawText));
                }
            }
            else if(args.Length > 0 && args[0] == "--generate-suggestions-incomplete")
            {
                var ss = new T().GetSuggestions(args.Skip(1).Rev().Skip(1).Rev()).Reduce();
                foreach(var s in ss)
                {
                    Console.WriteLine(string.Join(" ", s.RawText));
                }
            }
            else
                new T().Run(args);
        }

        public static bool WeakEquals(this string s, string t)
        {
            return s.Replace(" ", "").Equals(t.Replace(" ", ""));
        }

        public static string RemoveHeadTailSpaces(this string s)
        {
            return s.SkipWhile(char.IsWhiteSpace).Rev().SkipWhile(char.IsWhiteSpace).Rev().JoinToString();
        }

        public static void FailWith(string s, params object[] args)
        {
            Console.WriteLine(s, args);
            Environment.Exit(1);
        }

        public static void WriteLine(this string s, System.IO.TextWriter writer)
        {
            writer.WriteLine(s);
        }

        public static void WriteLine(this string s)
        {
            s.WriteLine(Console.Out);
        }

        public static List<string> SafeParse(this OptionSet o, IEnumerable<string> args)
        {
            return o.Try(xs => xs.Parse(args)).MatchEx(x => x, e =>
            {
                e.Match(
                    xe => Report.Fatal("{0}", e.Value.Message), 
                    () => Report.Fatal("failed to parse arguments.")
                );
                return null;
            });
        }

        public static string Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();   
            }
        }
    }
}

