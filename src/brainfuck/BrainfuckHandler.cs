﻿using System;
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

namespace Flame.Brainfuck
{
    public sealed class BrainfuckHandler : IProjectHandler
    {
        public IEnumerable<string> Extensions
        {
            get { return new string[] { "bf", "b" }; }
        }

        public IProject Parse(ProjectPath Path, ICompilerLog Log)
        {
            // Brainfuck "projects" always consist of a single file,
            // specified by the given path.
            return new SingleFileProject(Path, Log.Options.GetTargetPlatform());
        }

        public PassPreferences GetPassPreferences(ICompilerLog Log)
        {
            // We don't have any special preferences
            // for which passes to run.
            return new PassPreferences();
        }

        public IEnumerable<ParsedProject> Partition(IEnumerable<ParsedProject> Projects)
        {
            // We won't be partitioning the files we parsed,
            // because that doesn't really make sense for Brainfuck.
            return Projects;
        }

        public IProject MakeProject(IProject Project, ProjectPath Path, ICompilerLog Log)
        {
            // Turning a file into a project and writing it to disk
            // is not a useful concept for Brainfuck. For simplicity,
            // let's just return the input project.
            return Project;
        }

        public async Task<IAssembly> CompileAsync(IProject Project, CompilationParameters Parameters)
        {
            var binder = await Parameters.BinderTask;

            // Create an assembly.
            var resultAssembly = new DescribedAssembly(
                new SimpleName(Parameters.Log.GetAssemblyName(Project)),
                Parameters.Log.GetAssemblyVersion(new Version(1, 0, 0, 0)),
                binder.Environment);

            // Define a class called "Program".
            var programClass = new DescribedType(
                new SimpleName("Program"),
                resultAssembly);

            // Make class Program "public".
            programClass.AddAttribute(new AccessAttribute(AccessModifier.Public));

            // Make class Program "static".
            programClass.AddAttribute(PrimitiveAttributes.Instance.StaticTypeAttribute);

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

            // Add method Main to class Program.
            programClass.AddMethod(mainMethod);

            // Add class Program to the output assembly.
            resultAssembly.AddType(programClass);

            // Set the output assembly's entry point to method Main.
            resultAssembly.EntryPoint = mainMethod;

            return resultAssembly;
        }

        private IStatement GetMainBody(
            IProject Project, CompilationParameters Parameters,
            IBinder Binder)
        {
            // Fetch the source code.
            var code = ProjectHandlerHelpers.GetSourceSafe(
                Project.GetSourceItems().Single(), Parameters);

            if (code == null)
            {
                // Looks like the source code couldn't be retrieved.
                return new ReturnStatement();
            }

            // Resolve type `System.Console`.
            var consoleClass = Binder.BindType(
                new SimpleName("Console")
                .Qualify(
                    new SimpleName("System")
                    .Qualify()));

            if (consoleClass == null)
            {
                // We didn't manage to resolve `System.Console`.
                // Log a message and return a simple `return;` statement.

                Parameters.Log.LogError(new LogEntry(
                    "missing dependency",
                    "could not resolve type 'System.Console'."));

                return new ReturnStatement();
            }

            // Resolve `static void Write(char)` in `System.Console`
            var writeMethod = consoleClass.GetMethod(
                new SimpleName("Write"),
                true,
                PrimitiveTypes.Void,
                new IType[] { PrimitiveTypes.Char });

            if (writeMethod == null)
            {
                // We didn't manage to resolve `Write`.
                // Log a message and return a simple `return;` statement.

                Parameters.Log.LogError(new LogEntry(
                    "missing dependency",
                    "could not resolve method 'static void Write(char)'."));

                return new ReturnStatement();
            }

            // Resolve `static int Read()` in `System.Console`
            var readMethod = consoleClass.GetMethod(
                new SimpleName("Read"),
                true,
                PrimitiveTypes.Int32,
                new IType[] { });

            if (readMethod == null)
            {
                // We didn't manage to resolve `Read`.
                // Log a message and return a simple `return;` statement.

                Parameters.Log.LogError(new LogEntry(
                    "missing dependency",
                    "could not resolve method 'static int Read()'."));

                return new ReturnStatement();
            }

            return Brainfuck.Compile(
                code,
                Brainfuck.Initialize(
                    writeMethod, readMethod,
                    PrimitiveTypes.UInt8, 10000),
                Parameters.Log);
        }
    }
}
