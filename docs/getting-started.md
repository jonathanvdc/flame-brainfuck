
Building a Brainfuck compiler with Flame
========================================

## Getting started

For starters, fire up your favorite IDE and create a simple command-line project. Call it whatever you want; I called mine `flame-brainfuck` and set the default namespace to `Flame.Brainfuck`. Your main C# program file might look a bit like this.

```cs
using System;

namespace Flame.Brainfuck
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
```

Now, let's get Flame set up. This part shouldn't be too hard. All you have to do is install the [`Flame.Front` NuGet package](https://www.nuget.org/packages/Flame.Front/). That'll also pull in the necessary dependencies.

To get a working Flame-based "compiler," all we have to do is replace our "hello world" program by the following.

```cs
using System;
using Flame.Front.Cli;

namespace Flame.Brainfuck
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var compiler = new ConsoleCompiler(
                "flame-brainfuck", "my Flame-based Brainfuck compiler",
                "https://github.com/jonathanvdc/flame-brainfuck/releases");
            compiler.Compile(args);
        }
    }
}
```

You're welcome to replace `"flame-brainfuck"` by the name you picked for your project. If you build your project and produce an executable &ndash; which is, in my case, called `flame-brainfuck.exe` &ndash; then we can run the line prefixed by a dollar sign (`$`) below to get the given output.

```
$ ./flame-brainfuck.exe
flame-brainfuck: nothing to compile: no input files
```

You might even get colorized output, which is already plenty exciting in my book.

But wait, there's more. We can also print some version information, which looks like this on my machine.

```
$ ./flame-brainfuck.exe --version
flame-brainfuck version 1.0.6214 (based on Flame 0.9.1)
Platform: Unix 4.4.0.57
Console: xterm-256color
You can check for new releases at https://github.com/jonathanvdc/flame-brainfuck/releases
Thanks for using my Flame-based Brainfuck compiler! Have fun writing code.
flame-brainfuck: nothing to compile: no input files
```

The takeaway here is that Flame handles most, if not all, I/O. It'll happily parse command-line arguments for you and react to that on its own. You don't have to worry about the command-line interface; that's just not part of your core responsibilities as a compiler enthusiast, so handling typical user interaction is best left to a common library.
