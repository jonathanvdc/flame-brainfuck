
Building a Brainfuck compiler with Flame
========================================

## Hello world for cool kids

### Introduction and basic set-up

Most programming language tutorials invariably begin by teaching us how to
create a program that prints "Hello World!" So I figure it's only fitting that
we, too, start by creating a program that does just that.

There's just one tiny catch, though: we won't be coding a "hello world" program
directly &ndash; because where's the fun in that, right? Instead, we'll teach
our "compiler" to accept Brainfuck files (extensions `*.b` and `*.bf`) and
then produce a "hello world" program regardless of what they contain.
That's a pretty hacky trick, which is exactly what makes it a fun thing to do.

First off, create a new class and call it `BrainfuckHandler`, and paste
the following wall of includes in the file that defines it.

```cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Flame;
using Flame.Build;
using Flame.Compiler;
using Flame.Compiler.Expressions;
using Flame.Compiler.Projects;
using Flame.Compiler.Statements;
using Flame.Front;
using Flame.Front.Projects;
using Flame.Front.Target;
using Flame.Front.Options;
using Flame.Front.Passes;
```

> **Note:** We're using lots of namespaces because we're going to make
`BrainfuckHandler` do _everything_ &ndash; for real-life programming languages,
these namespace usings would be spread out across multiple files. But since
we're just trying to build a simple "hello world" program, we might as well just
make `BrainfuckHandler` do all the heavy lifting on its own. Hence the long list
of usings.

### Implementing `IProjectHandler`

Next, we'll have `BrainfuckHandler` implement `IProjectHandler`.

```cs
public sealed class BrainfuckHandler : IProjectHandler
{
```

`IProjectHandler` is an interface that defines what it means to implement a
language. An implementation of `IProjectHandler` handles the compilation process
for a programming language, which may have any number of file extensions
associated with it. Let's go over `IProjectHandler`'s members, and their
implementations in `BrainfuckHandler`.

  *  `IEnumerable<string> Extensions { get; }` specifies which file extensions
     are accepted by `BrainfuckHandler`. Its implementation is pretty
     straightforward.

     ```cs
     public IEnumerable<string> Extensions
     {
         get { return new string[] { "bf", "b" }; }
     }
     ```

  * `IProject Parse(ProjectPath Path, ICompilerLog Log)` interprets the file
    at `Path` as a project. A project is a collection of source files, library
    references, build options, etc. The important thing to note is that every
    project is compiled to exactly one assembly, i.e., a library or executable.
    A project can thus be considered as a blueprint of sorts for an assembly.

    A Brainfuck program is defined by exactly one source file, so we'll produce
    a single-file project here. Every project also needs a target platform
    string, which identifies the back-end that will be used to produce the
    compiler's output. We'll extract that from the command-line using an
    extension method on `Log.Options`.

    ```cs
    public IProject Parse(ProjectPath Path, ICompilerLog Log)
    {
        // Brainfuck "projects" always consist of a single file,
        // specified by the given path.
        return new SingleFileProject(Path, Log.Options.GetTargetPlatform());
    }
    ```

  * `PassPreferences GetPassPreferences(ICompilerLog Log)` allows us to extend
    the (optimization and diagnostics) pass pipeline with custom passes and
    conditions for these and/or existing passes to run.
    Pass preferences can be very useful, but we'll leave
    it alone for now and defer to the default pass pipeline's judgment.

    ```cs
    public PassPreferences GetPassPreferences(ICompilerLog Log)
    {
        // We don't have any special preferences
        // for which passes to run.
        return new PassPreferences();
    }
    ```

  * `IEnumerable<ParsedProject> Partition(IEnumerable<ParsedProject> Projects)`
    is method that allows us to do a bit of preprocessing after we've parsed
    all projects. Recall that every input file is parsed as a single project
    by `Parse`, and that every project is compiled to a single assembly.

    That's not the right compilation model for, say, a C# compiler.
    We want `csc A.cs B.cs` to produce a single assembly that includes the
    results of compiling both `A.cs` and `B.cs`. `Partition` allows us to fix
    that by merging the single-file projects for `A.cs` and `B.cs` into a
    single multi-file project.

    In our case, though, `Partition` is wholly irrelevant; Brainfuck files
    should not and cannot be combined. So we will do absolutely nothing here.

    ```cs
    public IEnumerable<ParsedProject> Partition(IEnumerable<ParsedProject> Projects)
    {
        // We won't be partitioning the files we parsed,
        // because that doesn't really make sense for Brainfuck.
        return Projects;
    }
    ```

  * `IProject MakeProject(IProject Project, ProjectPath Path, ICompilerLog Log)`
    is pretty niche. It enables front-ends to create multi-file projects from
    single-file projects. For example, a C# compiler might want to use
    `MakeProject` to create a `*.csproj` file from its inputs and command-line
    arguments.

    Again, we're not really interested in supplying this kind of functionality.

    ```cs
    public IProject MakeProject(IProject Project, ProjectPath Path, ICompilerLog Log)
    {
        // Turning a file into a project and writing it to disk
        // is not a useful concept for Brainfuck. For simplicity,
        // let's just return the input project.
        return Project;
    }
    ```

  * `Task<IAssembly> CompileAsync(IProject Project, CompilationParameters Parameters)`
    is where the magic happens. It takes a project and some useful information
    regarding the compilation task, and turns that into an assembly.

    Well, we'll actually be building an _intermediate representation_ (IR)
    assembly.
    Generating the actual output is the middle-end/back-end's problem, and
    happens automagically &ndash; in fact, that is exactly why you might want
    to use Flame in the first place.

    Implementing `CompileAsync` is still a relatively interesting endeavor,
    though. So we'll move that to its own section.

### Implementing `CompileAsync`
