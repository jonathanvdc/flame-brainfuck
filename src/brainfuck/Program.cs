using System;
using Flame.Front.Cli;
using Flame.Front.Projects;

namespace Flame.Brainfuck
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ProjectHandlers.RegisterHandler(new BrainfuckHandler());
            var compiler = new ConsoleCompiler(
                "flame-brainfuck", "my Flame-based Brainfuck compiler",
                "https://github.com/jonathanvdc/flame-brainfuck/releases");
            compiler.Compile(args);
        }
    }
}
