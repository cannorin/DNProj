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
                        Seq.Repeat(" ", 29).Iter(Console.Write);
                    }
                    else
                    {
                        Seq.Repeat(" ", 27 - s.Length).Iter(Console.Write);
                    }
                    Console.WriteLine(c.Value.ShortDescription);
                }
                Console.WriteLine("  help                       show this.");
            }

            var exams = this.GetExamples();
            if (exams.Any())
            {
                Console.WriteLine("\nexample:");
                Console.WriteLine(exams.JoinToString("\n"));
            }

            var tips = this.GetTips();
            if (tips.Any())
            {
                Console.WriteLine("\ntips:");
                Console.WriteLine(tips.JoinToString("\n"));
            }

            var warns = this.GetWarnings();
            if (warns.Any())
            {
                Console.WriteLine("\nwarning:");
                Console.WriteLine(warns.JoinToString("\n"));
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
                var c = rs.First();

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
                    Report.Error("command {0} not found.", c);
                    Help(rs);
                    Environment.Exit(1);
                }
            }
        }

        public virtual IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args)
        {
            return this.GenerateSuggestions(args);
        }

        public virtual IEnumerable<CommandSuggestion> GetChildSuggestions(ChildCommand child, IEnumerable<string> args)
        {
            throw new NotImplementedException("Implement this if your command has subcommands.");
        }

        protected Command Child(Action<IEnumerable<string>> f, string name, string desc, string sdesc, params string[] args)
        {
            return new ChildCommand(this, f, name, desc, sdesc, args);
        }
    }

    public static class CommandHelpParts
    {
        public static Dictionary<string, List<string>> Tips { get; set; }
        public static Dictionary<string, List<string>> Warnings { get; set; }
        public static Dictionary<string, List<string>> Examples { get; set; }

        public static Tuple<string, string, string> GetRandom()
        {
            if (Tips == null)
                Tips = new Dictionary<string, List<string>>();
            if (Warnings == null)
                Warnings = new Dictionary<string, List<string>>();
            if (Examples == null)
                Examples = new Dictionary<string, List<string>>();
            
            var tips = 
                Tips.Map(x => x.Value.Map(y => Tuple.Create("tips", x.Key, y)));
            var warns = 
                Warnings.Map(x => x.Value.Map(y => Tuple.Create("warning", x.Key, y)));
            var exams = 
                Examples.Map(x => x.Value.Map(y => Tuple.Create("example", x.Key, y)));

            var all = tips.Append(warns).Append(exams).Flatten().ToArray();

            return all[new Random().Next(0, all.Count())];
        }

        public static void AddTips(this Command c, string hint)
        {
            if (Tips == null)
                Tips = new Dictionary<string, List<string>>();
            if (!Tips.ContainsKey(c.Name))
                Tips[c.Name] = new List<string>();
            var h = hint.Split('\n')
                .Map(s => {
                    if(!s.StartsWith(" "))
                        return "  " + s;
                    else return s;
                })
                .JoinToString("\n");

            Tips[c.Name].Add(h);
        }

        public static List<string> GetTips(this Command c)
        {
            if (Tips == null)
                Tips = new Dictionary<string, List<string>>();
            if (!Tips.ContainsKey(c.Name))
                Tips[c.Name] = new List<string>();    
            return Tips[c.Name];
        }

        public static void AddWarning(this Command c, string warn)
        {
            if (Warnings == null)
                Warnings = new Dictionary<string, List<string>>();
            if (!Warnings.ContainsKey(c.Name))
                Warnings[c.Name] = new List<string>();
            var h = warn.Split('\n')
                .Map(s => {
                    if(!s.StartsWith(" "))
                        return "  " + s;
                    else return s;
                })
                .JoinToString("\n");

            Warnings[c.Name].Add(h);
        }

        public static List<string> GetWarnings(this Command c)
        {
            if (Warnings == null)
                Warnings = new Dictionary<string, List<string>>();
            if (!Warnings.ContainsKey(c.Name))
                Warnings[c.Name] = new List<string>();    
            return Warnings[c.Name];
        }

        public static void AddExample(this Command c, string exam)
        {
            if (Examples == null)
                Examples = new Dictionary<string, List<string>>();
            if (!Examples.ContainsKey(c.Name))
                Examples[c.Name] = new List<string>();
            var h = exam.Split('\n')
                .Map(s => {
                    if(!s.StartsWith(" "))
                        return "  " + s;
                    else return s;
                })
                .JoinToString("\n");

            Examples[c.Name].Add(h);
        }

        public static List<string> GetExamples(this Command c)
        {
            if (Examples == null)
                Examples = new Dictionary<string, List<string>>();
            if (!Examples.ContainsKey(c.Name))
                Examples[c.Name] = new List<string>();    
            return Examples[c.Name];
        }
    }

    public class ChildCommand : Command
    {
        Action<IEnumerable<string>> a;

        Command p;

        public ChildCommand(Command parent, Action<IEnumerable<string>> f, string name, string desc, string sdesc, params string[] args)
            : base(name, desc, sdesc, args)
        {
            a = f;
            Options = parent.Options;
            p = parent;
        }

        public override void Run(IEnumerable<string> args)
        {
            a(args);
        }

        public override IEnumerable<CommandSuggestion> GetSuggestions(IEnumerable<string> args)
        {
            return p.GetChildSuggestions(this, args);
        }

        public override void Help(IEnumerable<string> args)
        {
            Console.WriteLine(@"usage: {0} {1}

{2}

Please see parent command's help for details.", Name, string.Join(" ", Args), Description);
        }
    }
}
