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
using NuGet;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Resources;

namespace DNProj
{
    public class NuGetSearchCommand : Command
    {
        Option<int> count;
        string sourceUrl = "https://packages.nuget.org/api/v2";

        public NuGetSearchCommand(string name)
            : base(name, "search NuGet packages from the official NuGet repository.", "search NuGet packages.", "[name]")
        {
            Options.Add("c=|count=", "show top N results. [default=10]", s => count = s.Try(int.Parse));
            Options.Add("custom-source=", "use custom NuGet source. only the NuGet v2 endpoint can be used.", p => sourceUrl = p);
        }

        public override void Run(IEnumerable<string> _args)
        {
            try
            {
                var args = Options.SafeParse(_args);

                if (args.Any())
                {
                    var name = args.JoinToString(" ");
                    var take = count.Default(10);
                    var repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl);
                    var res = repo.Search(name, false);
                    if (res.Count() > 0)
                    {
                        repo.UpdatePackageNamesCache(res);
                        Console.WriteLine("Search result for '{0}' from {1}.", name, sourceUrl);
                        Console.WriteLine();
                        foreach (var p in res.Take(take))
                        {
                            Console.WriteLine("------------");
                            Console.WriteLine();
                            Console.WriteLine(
                                @"* {0}
  Ver.{5} / {4} Downloads
  Description: 
    {1}
  Url: {3}
  Tags: {2}"
                        , p.Title, p.Description.Split('\n').JoinToString("\n    "), p.Tags, p.ProjectUrl, p.DownloadCount, p.Version);
                            if (p.RequireLicenseAcceptance)
                            {
                                ConsoleNX.ColoredWriteLine("  This package requires license acceptance.", ConsoleColor.Yellow);
                                ConsoleNX.ColoredWrite("  License url: ", ConsoleColor.Yellow);
                                Console.WriteLine(p.LicenseUrl);
                            }
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Report.Error("No more results found for '{0}'.", name);
                    }
                }
                else
                {
                    Report.Error("name missing.");
                    Console.WriteLine();
                    Help(args);
                }
            }
            catch (NullReferenceException)
            {
                throw;
            }
            catch (Exception e)
            {
                Report.Fatal(e.Message);
            }
        }
    }
}

