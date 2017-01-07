# Hello world for cool kids

## Introduction and basic set-up

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

## Implementing `IProjectHandler`

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

## Implementing `CompileAsync`

`CompileAsync` is where things start to get interesting. This is where we'll
generate an assembly that contains the following "hello world" Program.

```cs
public static class Program
{
    public static void Main(string[] args)
    {
        System.Console.WriteLine("Hello World!");
        return;
    }
}
```

So we want to generate the following hierarchy of constructs:
  * an assembly, which defines:
    * a `public static class` called "Program", which defines:
      * a method `public static void Main(string[] args)`.

We'll also need to get a handle to `System.Console.WriteLine` somehow to write
`Main`'s body. Flame defines the `IBinder` interface as the primary means with
which you can resolve type names, i.e., you give it a type name like
`System.Console`, and the `IBinder` returns an `IType` that you can query
for methods, fields, properties, base classes, attributes, etc.

We can get an `IBinder` implementation for the union of all external
dependencies by waiting for the `CompilationParameters.BinderTask` to complete,
like so:

```cs
public async Task<IAssembly> CompileAsync(IProject Project, CompilationParameters Parameters)
{
    var binder = await Parameters.BinderTask;
```

Now, let's define the assembly we intend to produce. We want its name to
correspond to the assembly name defined by a command-line argument or the project,
its version to be either a version number specified by a command-line argument
or `1.0.0.0`, and its environment (`IEnvironment`) to be equal to the runtime's
environment.

> `IEnvironment` is an interface that can be queried for information about the
  runtime, and in particular its type system.
  For example, `IEnvironment.RootType` returns the root type, if any, for
  the currently selected runtime. For our purposes, we'll only use `IEnvironment`
  as an argument to the assembly.

```cs
// Create an assembly.
var resultAssembly = new DescribedAssembly(
    new SimpleName(Parameters.Log.GetAssemblyName(Project)),
    Parameters.Log.GetAssemblyVersion(new Version(1, 0, 0, 0)),
    binder.Environment);
```

> Notice that we've created a `DescribedAssembly`. In Flame, any type that
  starts with the `Described` prefix indicates that objects of that type are
  constructed in an imperative manner.
>
> More advanced front-ends might want to handle things differently. For example,
  they might populate an assembly by lazily analyzing an Abstract Syntax Tree
  (AST) instead of adding members directly.  

The next bullet in our list of constructs to define is `class Program`. This
one's actually pretty easy: we first define a type called `Program` in our
newly-created assembly, then slap attributes onto it to mark it as
`public` and `static`.

```cs
// Define a class called "Program".
var programClass = new DescribedType(
    new SimpleName("Program"),
    resultAssembly);

// Make class Program "public".
programClass.AddAttribute(new AccessAttribute(AccessModifier.Public));

// Make class Program "static".
programClass.AddAttribute(PrimitiveAttributes.Instance.StaticTypeAttribute);
```

We can now define our `Main` method. This'll be slightly more challenging than
defining the `Program` type, but not by much.

```cs
// Define a method called "Main" in class Program.
var mainMethod = new DescribedBodyMethod(
    new SimpleName("Main"),
    programClass);

// Make method Main "public".
mainMethod.AddAttribute(new AccessAttribute(AccessModifier.Public));

// Make method Main "static".
mainMethod.IsStatic = true;

// Set method Main's return type to "void".
mainMethod.ReturnType = PrimitiveTypes.Void;

// Add a parameter 'string[] args' to method Main.
mainMethod.AddParameter(
    new DescribedParameter(
        "args", PrimitiveTypes.String.MakeArrayType(1)));

// Set method Main's method body to a statement generated by
// GetMainBody.
mainMethod.Body = GetMainBody(Project, Parameters, binder);
```

There are two things of note here:
  1. Marking our `Main` method as `static` is accomplished by setting a Boolean
     property, but marking `class Program` as static is done by adding an
     attribute to `class Program`. This is because `Main` is a member and
     `Program` is a type; being `static` has far-reaching implications for
     members, but for types it's actually fairly irrelevant.

  2. We rely on `GetMainBody` to generate a method body for us.
     Don't worry; we'll define `GetMainBody` shortly.

We're almost done with writing `CompileAsync`. All we have to do now is some
bookkeeping: `class Program` needs to know that it defines the `Main` method,
the resulting assembly needs to be made aware that it defines `class Program`,
and we should set its entry point needs to be set to `Main`.

```cs
// Add method Main to class Program.
programClass.AddMethod(mainMethod);

// Add class Program to the output assembly.
resultAssembly.AddType(programClass);

// Set the output assembly's entry point to method Main.
resultAssembly.EntryPoint = mainMethod;

return resultAssembly;
```

## One more thing: `GetMainBody`

And then there is only the small matter of getting `Main` to print
`"Hello World!"` Our algorithm to do just that will consist of the following
three steps:

  1. Resolve `System.Console`. In other words, turn the `System.Console` type
     name into an `IType`-implementing object that we can inspect.

  2. Find method `void WriteLine(string)` defined by `System.Console`.

  3. Create an `IStatement` instance that calls `System.Console.WriteLine` with
     a string literal argument, ignores the result (which is `void`), and
     returns.

Note that steps one and two can fail: the standard library may not define a
type called `System.Console`, and even if it does, `System.Console` may not
define a method with signature `void WriteLine(string)`. We need to check for
that, and log an error when appropriate.

Let's start with the first step.

```cs
private IStatement GetMainBody(
    IProject Project, CompilationParameters Parameters,
    IBinder Binder)
{
    // Resolve type `System.Console`.
    var consoleClass = Binder.BindType(
        new SimpleName("Console")
        .Qualify(
            new SimpleName("System")
            .Qualify()));
```

> The code for `Binder.BindType`'s argument looks fairly convoluted. This is
  because `BindType` takes a `QualifiedName`, which is essentially a linked
  list of unqualified names. In the code above, we start off with a `SimpleName`
  (`new SimpleName("Console")`), and then qualify it with a qualified name
  which contains a single unqualified name (`new SimpleName("System").Qualify()`).
>
> Usually, this problem doesn't arise, because type names in programming
  languages are typically translated to qualified names in a piecewise fashion.
  We do have to deal with it here because we're baking the code for resolving
  `System.Console` into our compiler.

But what if there is no type named `System.Console`? In that case, `BindType`
will return `null`. We ought to check for that, and let the user know what's
up if and when we can't find a type named `System.Console`.

```cs
if (consoleClass == null)
{
    // We didn't manage to resolve `System.Console`.
    // Log a message and return a simple `return;` statement.

    Parameters.Log.LogError(new LogEntry(
        "missing dependency",
        "could not resolve type 'System.Console'."));

    return new ReturnStatement();
}
```

> `Parameters.Log` implements the `ICompilerLog` interface. The `ICompilerLog`
  interface is the primary means for Flame-based compilers to do I/O: the
  `ICompilerLog.Options` property can be queried for (command-line) options,
  and `LogError`, `LogWarning`, `LogMessage` and `LogEvent` methods can be
  used to present the user with diagnostics.

Our next step is to find the `WriteLine` method. This can be done like so:

```cs
// Resolve `static void WriteLine(string)` in `System.Console`
var writeLineMethod = consoleClass.GetMethod(
    new SimpleName("WriteLine"),
    true,
    PrimitiveTypes.Void,
    new IType[] { PrimitiveTypes.String });
```

You should read the arguments to the method call above as: "find a method
called `WriteLine`, which is `static`, has return type `void`, and takes a
single argument of type `string`." As before, failure to do this results in
a `null` return value, which we can handle gracefully.

```cs
if (writeLineMethod == null)
{
    // We didn't manage to resolve `WriteLine`.
    // Log a message and return a simple `return;` statement.

    Parameters.Log.LogError(new LogEntry(
        "missing dependency",
        "could not resolve method 'static void System.Console.WriteLine(string)'."));

    return new ReturnStatement();
}
```

We can now proceed to _actually generating `Main`'s method body._ We want to
generate a composite (block) statement that consists of two child statements:
  * an expression statement, which ignores the result of a call to
    `System.Console.WriteLine` with no receiver object and a single string literal
    argument, and
  * a return statement.

```cs
var statements = new IStatement[]
{
    // `System.Console.WriteLine("Hello, world!");`
    new ExpressionStatement(
        new InvocationExpression(
            writeLineMethod, null,
            new IExpression[]
            {
                new StringExpression("Hello World!")
            })),

    // `return;`
    new ReturnStatement()
};

// Bundle the generated statements in a block.
return new BlockStatement(statements);
```

> **Note:** unlike with programming languages such as C#, the return statement
  _cannot_ be omitted. Doing so anyway will result in an invalid method body,
  which will in all likelihood only be diagnosed at run-time.

And that's all there is to it. We now have an `IProjectHandler` that will
take Brainfuck files as input, only to ignore them completely and then
generate a "hello world" program instead. Lovely.

## One last statement: registering `BrainfuckHandler`

Having an `IProjectHandler` for Brainfuck doesn't imply that our compiler will
actually recognize and compile Brainfuck files. In fact, if you were to compile
and then run our compiler with input `tests/mirror/mirror.bf -platform clr`
(which you may read as "compile tests/mirror/mirror.bf for the CLR platform"),
you'd get the output below.

```
$ ./flame-brainfuck.exe tests/mirror/mirror.bf -platform clr
error: invalid extension: extension 'bf' in 'tests/mirror/mirror.bf' was not recognized as a known project extension.
Supported extensions:
 * flo
 * fir
```

The problem is that Flame has not been informed that `BrainfuckHandler` is a
thing. Fortunately, the fix is fairly easy. Prepend the following statement
to the body of your compiler's `Main` method.

```cs
ProjectHandlers.RegisterHandler(new BrainfuckHandler());
```

Your `Main` method should look like this now.

```cs
public static void Main(string[] args)
{
    ProjectHandlers.RegisterHandler(new BrainfuckHandler());
    var compiler = new ConsoleCompiler(
        "flame-brainfuck", "my Flame-based Brainfuck compiler",
        "https://github.com/jonathanvdc/flame-brainfuck/releases");
    compiler.Compile(args);
}
```

Let's try "compiling" a Brainfuck program again now. The following command
should have no output at all.

```
$ ./flame-brainfuck.exe tests/mirror/mirror.bf -platform clr
```

Instead, it will make a directory named `bin` appear in `tests/mirror`. `bin`
will contain a CLR executable named `mirror.exe`, which we can run.

```
$ ./tests/mirror/bin/mirror.exe
Hello World!
```  

## Wrapping up

We have succeeded in building a compiler that takes Brainfuck files as input and
produces "hello world" programs as output. By this point, our compiler
is slightly smaller than 200 lines of code, including comments and whitespace.

If you got stuck at some point, or if you just want to take a look at the
complete source code, then the you're welcome to browse
[`flame-brainfuck`'s source code on GitHub.](https://github.com/jonathanvdc/flame-brainfuck/tree/master/src/hello-world)

Eventually, we will replace `GetMainBody`'s code with logic that reads and compiles Brainfuck files. But let's play around with our compiler first.
