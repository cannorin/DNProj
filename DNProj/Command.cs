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
using DNProj;

namespace DNProj
{
    public abstract class Command
    {
        public Dictionary<string, Command> Commands;
        public OptionSet Options;

        public string Name { get; set; }

        public string Description { get; set; }

        public string ShortDescription { get; set; }

        public string[] Args { get; set; }

        public Command(string name, string desc, string shortdesc, params string[] args)
        {
            Options = new OptionSet();
            Commands = new Dictionary<string, Command>();
            Name = name;
            Description = desc;
            ShortDescription = shortdesc;
            Args = args;
        }

        public virtual void Help(IEnumerable<string> args)
        {
            Console.WriteLine(@"usage: {0} {1}

{2}", Name, string.Join(" ", Args), Description);
            if (Commands.Any())
            {
                Console.WriteLine("\ncommands:");
                foreach (var c in Commands)
                {
                    var s = c.Key + " " + string.Join(" ", c.Value.Args);
                    Console.Write("  {0}", s);
                    if (27 - s.Length <= 0)
                    {
                        Console.WriteLine();
                        IENX.Repeat(" ", 29).Iter(Console.Write);
                    }
                    else
                    {
                        IENX.Repeat(" ", 27 - s.Length).Iter(Console.Write);
                    }
                    Console.WriteLine(c.Value.ShortDescription);
                }
                Console.WriteLine("  help                       show this.");
            }
            Console.WriteLine("\noptions:");
            Options.WriteOptionDescriptions(Console.Out);
            Console.WriteLine("  -h, -?, --help             show this.");
        }

        /// <summary>
        /// If you override this, do not forget to do <code>Options.Parse(args);</code>
        /// </summary>
        /// <param name="args">Arguments.</param>
        public virtual void Run(IEnumerable<string> args)
        {
            var rs = Options.SafeParse(args);
            if (rs.Count == 0)
                Help(rs);
            else
            {
                var c = rs.Hd();

                if (Commands.ContainsKey(c))
                    Commands[c].Run(rs.Skip(1));
                else if (Commands.ContainsKey("this"))
                {
                    Commands["this"].Run(rs);
                }
                else if (Templates.HelpOptions.Contains(c))
                {
                    Help(rs);
                }
                else
                {
                    Console.WriteLine("command {0} not found.", c);
                    Help(rs);
                    Environment.Exit(1);
                }
            }
        }
    }

    public class SimpleCommand : Command
    {
        Action<IEnumerable<string>> a;

        public SimpleCommand(Action<IEnumerable<string>> f, string name, string desc, string sdesc, params string[] args)
            : base(name, desc, sdesc, args)
        {
            a = f;
        }

        public override void Run(IEnumerable<string> args)
        {
            a(args);
        }

        public override void Help(IEnumerable<string> args)
        {
            Console.WriteLine(@"usage: {0} {1}

{2}

for options, please view parent command's help.", Name, string.Join(" ", Args), Description);
        }
    }
}
