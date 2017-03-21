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
using System.Diagnostics;
using Mono.Options;
using Microsoft.Build.BuildEngine;

namespace DNProj
{
    public class CommandSuggestion
    {
        public string[] RawText { get; private set;}

        /// <summary>
        /// use helper methods instead
        /// </summary>
        /// <param name="rawText">Raw text.</param>
        public CommandSuggestion(IEnumerable<string> rawText)
        {
            RawText = rawText.ToArray();
        }

        /// <summary>
        /// use helper methods instead
        /// </summary>
        /// <param name="rawText">Raw text.</param>
        public CommandSuggestion(params string[] rawText)
        {
            RawText = rawText.ToArray();
        }

        static IEnumerable<string> mapQuote(IEnumerable<string> seq)
        {
            return seq.Map(x => string.Format("'{0}'", escape(x)));
        }

        static string escape(string s)
        {
            return s.Replace("'", "'\"'\"'").Replace(":", "\\:").Replace("\\", "\\\\");
        }

        public static CommandSuggestion Values(IEnumerable<string> args)
        {
            if (args.Count() == 0)
                return CommandSuggestion.None;
            else return new CommandSuggestion(New.Seq("_values", "'values'").Concat(mapQuote(args)).ToArray());
        }

        public static CommandSuggestion Values(params string[] args)
        {
            return CommandSuggestion.Values(args.AsEnumerable());
        }

        public static CommandSuggestion Files(Option<string> filter = default(Option<string>))
        {
            return filter.Match(
                x => new CommandSuggestion("_files", "-g", string.Format("'{0}'", x)),
                () => new CommandSuggestion("_files")
            );
        }

        public static CommandSuggestion Directories(Option<string> filter = default(Option<string>))
        {
            return filter.Match(
                x => new CommandSuggestion("_files", "-/", "-g", string.Format("'{0}'", x)),
                () => new CommandSuggestion("_files", "-/")
            );
        }

        public static CommandSuggestion Option(string description, IEnumerable<string> aliases)
        {
            if (aliases.Count() == 1)
            {
                var f = aliases.First();
                return new CommandSuggestion("_arguments", string.Format("'{0}[{1}]'", f, escape(description), "\"*: :->hoge\""));
            }
            else
            {
                return new CommandSuggestion("_arguments", string.Format("{{{0}}}'[{1}]'", aliases.JoinToString(","), escape(description)), "\"*: :->hoge\"");
            }
        }

        public static CommandSuggestion Option(string description, params string[] aliases)
        {
            return CommandSuggestion.Option(description, aliases.AsEnumerable());
        }

        public static CommandSuggestion None 
        {
            get
            {
                return new CommandSuggestion(":");
            }
        }
    }

    public static class CommandSuggestionTools
    {
        public static IEnumerable<CommandSuggestion> Reduce(this IEnumerable<CommandSuggestion> cs)
        {
            if (cs.Count() == 0)
                return CommandSuggestion.None.Singleton();
            else
                return cs.GroupBy(x => x.RawText[0])
                    .Map(c => 
                    {
                        if (c.Key == "_arguments")
                        {
                            return new CommandSuggestion(
                                c.Key.Singleton()
                                     .Concat(c.Map(x => x.RawText.Skip(1).Rev().Skip(1).Rev()).Flatten())
                                     .Concat("\"*: :->hoge\"".Singleton()));
                        }
                        else
                            return new CommandSuggestion(c.Key.Singleton().Concat(c.Map(x => x.RawText.Skip(1)).Flatten()));
                    });
        }

        public static CommandSuggestion Merge(this IEnumerable<CommandSuggestion> cs)
        {
            if (cs.Count() == 0)
                return CommandSuggestion.None;
            else if (cs.Count() == 1)
                return cs.First();
            else
                return new CommandSuggestion(":;".Singleton().Concat(cs.Map(x => x.RawText.Concat(";".Singleton())).Flatten()));
        }

        public static IEnumerable<CommandSuggestion> GenerateSuggestions(
            this Command c, 
            IEnumerable<string> args,
            Func<string, CommandSuggestion> incompleteOption = null,
            Func<CommandSuggestion> noArgs = null,
            Func<string[], CommandSuggestion> withArgs = null,
            Option<string> incompleteInput = default(Option<string>)
        )
        {
            var _rs = c.Options.Try(xs => xs.Parse(args));
            if(_rs.HasException)
            {
                if (incompleteOption != null)
                    yield return incompleteOption(args.Last());
            }
            else
            {
                if (!_rs.Value.Any())
                {
                    if (c.Commands.Keys.Any())
                        yield return CommandSuggestion.Values(c.Commands.Keys);
                    foreach (var o in c.Options)
                    {
                        var os = o.GetNames().Map(x => (x.Length == 1 ? "-" : "--") + x);
                        if (!o.Hidden && !os.Any(args.Contains))
                        {
                            var desc = o.Description.Split(true, ". ", ", ")[0];
                            if (desc.EndsWith("."))
                                desc = desc.Remove(desc.Length - 1);
                            yield return CommandSuggestion.Option(
                                desc,
                                os
                            );
                        }
                    }
                    if (noArgs != null)
                        yield return noArgs();
                }
                else
                {
                    var rs = _rs.Value.ToArray();
                    foreach (var o in c.Options)
                    {
                        var os = o.GetNames().Map(x => (x.Length == 1 ? "-" : "--") + x);
                        if (!o.Hidden && !os.Any(args.Contains))
                        {
                            var desc = o.Description.Split(true, ". ", ", ")[0];
                            if (desc.EndsWith("."))
                                desc = desc.Remove(desc.Length - 1);
                            yield return CommandSuggestion.Option(
                                desc,
                                os
                            );
                        }
                    }
                    if (c.Commands.Keys.Contains(rs[0]))
                        foreach (var o in c.Commands[rs[0]].GetSuggestions(rs.Skip(1), incompleteInput))
                            yield return o;
                    else if (withArgs != null)
                        yield return withArgs(rs);
                }
            }
        }
    }
}

