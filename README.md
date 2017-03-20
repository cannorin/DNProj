DNProj
======

[![Build Status](https://travis-ci.org/cannorin/DNProj.svg?branch=master)](https://travis-ci.org/cannorin/DNProj)

Manage your .\*proj and .sln from commandline.

# dnproj

Use ``` dnproj ``` to edit your MSBuild/XBuild projects (\*proj files).

For example:

``` dnproj new ConsoleApplication1 -t csharp ```

to create a new project with "Hello, World!" template, written in C#.

``` dnproj add SomeClass.cs ```

to add existing .cs file to your project.

``` dnproj add-ref System.Numerics ```

to add a reference to your project.

Then...

``` xbuild ```

to build your project.

# dnproj NuGet integration

You can easily install, update, remove, and restore NuGet packages.

``` dnproj nuget install NuGet.Core EntityFramework:5.0.0 ```

``` dnproj nuget update ```

``` dnproj nuget remove EntityFramework ```

``` dnproj nuget restore ```

Also, you can perform a quick search on the NuGet repository.

```
$ dnproj nuget search "Reactive Extensions"
Search result for 'Reactive Extensions' from https://packages.nuget.org/api/v2.

------------

* System.Threading.Tasks.Extensions
  Ver.4.3.0 / 3743690 Downloads
  Description:
    Provides additional types that simplify the work of writing concurrent and asynchronous code.

    Commonly Used Types:
    System.Threading.Tasks.ValueTask<TResult>
  Url: https://dot.net/
  Tags:
  This package requires license acceptance.
  License url: http://go.microsoft.com/fwlink/?LinkId=329770

------------

* My useful extensions pack
  Ver.1.0.5 / 2047 Downloads
  Description:
    Extensions, sorted by namespaces
  Url:
  Tags: c# extensions .net

------------

* System.Reflection.Extensions
  Ver.4.3.0 / 7509987 Downloads
  Description:
    Provides custom attribute extension methods for System.Reflection types.

    Commonly Used Types:
    System.Reflection.InterfaceMapping
    System.Reflection.CustomAttributeExtensions
    System.Reflection.RuntimeReflectionExtensions

    When using NuGet 3.x this package requires at least version 3.4.
  Url: https://dot.net/
  Tags:
  This package requires license acceptance.
  License url: http://go.microsoft.com/fwlink/?LinkId=329770

...
```

# dnsln

work in progress.

concepts:

``` dnsln new ConsoleApplication1.sln ```

``` dnsln add-proj ConsoleApplication1.csproj ```

``` dnsln add SomeSolutionItem.txt ```

# build and install

DNProj itself requires Mono 3.x or later. 

Building DNProj requires ```make```, ```git``` and ```curl```.

Just type:

```bash
PREFIX=/path/to/your/destination make install
```
# zsh completion

Copy ```misc/zsh-completion/_dnproj``` to your ```$FPATH```.

Make sure ```dnproj``` is in your ```$PATH``` before use.

# license

...is GPL v3.

Any files generated by this software will not be licensed under GPL.

DNProj uses a template licensed under the X11 license, to generate project files and source code files. See DNProj/Templates.cs.
