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
using System.IO;
using Mono.Options;
using NX;
using DNProj;

namespace DNProj
{
    public class ManGenerator
    {
        public static string BuildFromCommand(
                Command c,
                int section, 
                Option<string> date = default(Option<string>),
                Option<string> source = default(Option<string>),
                Option<string> manual = default(Option<string>)
                )
        {
            var title = c.Name.Replace(' ', '-');
            var sb = new StringBuilderNX();
            if((date || source || manual).HasValue)
                sb.WriteLine(".TH \"{0}\" {1} \"{2}\" \"{3}\" \"{4}\"", 
                        title, section,
                        date.DefaultLazy(() => DateTimeOffset.Now.ToString()),
                        source.Default("Man Page"),
                        manual.Default("")
                        );
            else
                sb.WriteLine(".TH \"{0}\" {1}", title, section); 

            sb.WriteLine();

            sb.WriteLine(".SH NAME");
            sb.WriteLine(c.Name + " - " + c.ShortDescription.Replace("'", "\\'"));
            sb.WriteLine();

            sb.WriteLine(".SH SYNOPSIS");
            sb.WriteLine(".B {0}", c.Name);
            sb.WriteLine(string.Join(" ", c.Args));
            sb.WriteLine();

            sb.WriteLine(".SH DESCRIPTION");
            sb.WriteLine(".PP");
            sb.WriteLine(c.Description.Replace("\\", "\\\\").Replace("'", "\\'"));
            sb.WriteLine();

            if (c.Commands.Any())
            {
                sb.WriteLine(".SH SUBCOMMANDS");
                foreach (var sc in c.Commands)
                {
                    var s = sc.Key + " " + string.Join(" ", sc.Value.Args);
                    sb.WriteLine(".TP");
                    sb.WriteLine(".B {0}", s);
                    sb.WriteLine(sc.Value.ShortDescription.Replace("'", "\\'"));
                }
            }
            sb.WriteLine();

            if(c.Options.Any())
            {
                sb.WriteLine(".SH OPTIONS");
                foreach(var o in c.Options)
                {
                    var os = o.GetNames().Map(x => (x.Length == 1 ? "-" : "--") + x);
                    sb.WriteLine(".TP");
                    sb.WriteLine(string.Format(".B {0}", os.JoinToString(", ")));
                    sb.WriteLine(o.Description.Replace("\\", "\\\\").Replace("'", "\\'"));
                }
                sb.WriteLine();
            } 

            var exams = c.GetExamples();
            if (exams.Any())
            {
                sb.WriteLine(".SH EXAMPLE");
                exams.Map(x => " " + x.RemoveHeadTailSpaces().Replace("\\", "\\\\").Replace("'", "\\'")).Iter(sb.WriteLine);
            }

            var tips = c.GetTips();
            if (tips.Any())
            {
                sb.WriteLine(".SH TIPS");
                tips.Map(x => " " + x.RemoveHeadTailSpaces().Replace("\\", "\\\\").Replace("'", "\\'")).Iter(sb.WriteLine);
            }

            var warns = c.GetWarnings();
            if (warns.Any())
            {
                sb.WriteLine(".SH WARNING");
                warns.Map(x => " " + x.RemoveHeadTailSpaces().Replace("\\", "\\\\").Replace("'", "\\'")).Iter(sb.WriteLine);
            }

            if (c.Commands.Any())
            {
                sb.WriteLine(".SH SEE ALSO");
                sb.WriteLine(".sp");
                var s = c.Commands.Map(x => "\\fB" +  x.Value.Name.Replace(' ', '-') + "\\fR(" + section + ")").JoinToString(", ");
                sb.WriteLine(s);
            }
 
            return sb.ToString(); 
        }
        public static void WriteAllCommands(Command c, string path, int section = 0)
        {
            var dest = Path.Combine(path, "share/man/man" + (section == 0 ? "l" : section.ToString()));
            var fn = c.Name.Replace(' ', '-') + "." + (section == 0 ? "l" : section.ToString());

            if(!Directory.Exists(dest))
                Directory.CreateDirectory(dest);

            var s = BuildFromCommand(c, section);

            File.WriteAllText(Path.Combine(dest, fn), s);

            foreach(var sc in c.Commands)
                WriteAllCommands(sc.Value, path, section);
        }

    }

}

