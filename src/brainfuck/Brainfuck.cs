using System;
using System.Linq;
using Flame.Compiler;
using Flame.Compiler.Expressions;
using Flame.Compiler.Variables;
using Flame.Compiler.Statements;

namespace Flame.Brainfuck
{
    public static class Brainfuck
    {
        public static BrainfuckState Initialize(
            IMethod PrintMethod, IMethod ReadMethod,
            IType ElementType, int ArraySize)
        {
            var arrayVar = new LocalVariable(
                "data", ElementType.MakeArrayType(1));

            var indexVar = new LocalVariable(
                "index", PrimitiveTypes.Int32);

            var state = new BrainfuckState(
                PrintMethod, ReadMethod, arrayVar, indexVar);

            // Push a single block onto the stack.
            state.PushBlock();

            // index = 0;
            state.Append(
                indexVar.CreateSetStatement(
                    new IntegerExpression(0)));

            // data = new ElementType[ArraySize];
            state.Append(
                arrayVar.CreateSetStatement(
                    new NewArrayExpression(
                        ElementType,
                        new IExpression[] 
                        { 
                            new IntegerExpression(ArraySize) 
                        })));

            return state;
        }

        private static IStatement Increment(
            IVariable Variable)
        {
            // Variable = Variable + (decltype(Variable))1;
            return Variable.CreateSetStatement(
                new AddExpression(
                    Variable.CreateGetExpression(),
                    new StaticCastExpression(
                        new IntegerExpression(1),
                        Variable.Type)
                    .Simplify()));
        }

        private static IStatement Decrement(
            IVariable Variable)
        {
            // Variable = Variable - (decltype(Variable))1;
            return Variable.CreateSetStatement(
                new SubtractExpression(
                    Variable.CreateGetExpression(),
                    new StaticCastExpression(
                        new IntegerExpression(1),
                        Variable.Type)
                    .Simplify()));
        }

        private static IStatement Print(BrainfuckState State)
        {
            // Print((Print_Parameter0_Type)array[index]);
            return new ExpressionStatement(
                new InvocationExpression(
                    State.PrintMethod,
                    null,
                    new IExpression[]
                    {
                        new StaticCastExpression(
                            State.ElementVariable.CreateGetExpression(),
                            State.PrintMethod.Parameters.First().ParameterType)
                    }));
        }

        private static IStatement Read(BrainfuckState State)
        {
            // var tmp = Read();
            // array[index] = tmp > 0 ? (ElementType)tmp : 0;
            var tmpVar = new LocalVariable("tmp", State.ReadMethod.ReturnType);
            return new BlockStatement(new IStatement[]
            {
                tmpVar.CreateSetStatement(
                    new InvocationExpression(
                        State.ReadMethod, null, 
                        new IExpression[] { })),
                State.ElementVariable.CreateSetStatement(
                    new SelectExpression(
                        new GreaterThanExpression(
                            tmpVar.CreateGetExpression(),
                            new StaticCastExpression(
                                new IntegerExpression(0),
                                tmpVar.Type)),
                        new StaticCastExpression(
                            tmpVar.CreateGetExpression(),
                            State.ElementVariable.Type),
                        new StaticCastExpression(
                            new IntegerExpression(0),
                            State.ElementVariable.Type)))
            });
        }

        private static IStatement PopLoop(
            BrainfuckState State, ISourceDocument Code,
            int Index, ICompilerLog Log)
        {
            if (State.BlockDepth <= 1)
            {
                // Log an error, return the empty statement.
                Log.LogError(new LogEntry(
                    "unexpected character", 
                    "the given left bracket character (']') " +
                    "doesn't have a matching right bracket ('[') " +
                    "to precede it.",
                    new SourceLocation(Code, Index)));
                
                return EmptyStatement.Instance;
            }

            // while (array[index] != 0) { ... }
            return new WhileStatement(
                new InequalityExpression(
                    State.ElementVariable.CreateGetExpression(),
                    new StaticCastExpression(
                        new IntegerExpression(0),
                        State.ElementVariable.Type)),
                State.PopBlock());
        }

        public static IStatement Compile(
            ISourceDocument Code, BrainfuckState State,
            ICompilerLog Log)
        {
            string source = Code.Source;
            int index = 0;

            while (index < source.Length)
            {
                char c = source[index];
                switch (c)
                {
                    case '>':
                        State.Append(Increment(State.IndexVariable));
                        break;
                    case '<':
                        State.Append(Decrement(State.IndexVariable));
                        break;
                    case '+':
                        State.Append(Increment(State.ElementVariable));
                        break;
                    case '-':
                        State.Append(Decrement(State.ElementVariable));
                        break;
                    case '.':
                        State.Append(Print(State));
                        break;
                    case ',':
                        State.Append(Read(State));
                        break;
                    case '[':
                        State.PushBlock();
                        break;
                    case ']':
                        State.Append(PopLoop(State, Code, index, Log));
                        break;
                    default:
                        // Ignore all other characters.
                        break;
                }
                index++;
            }

            if (State.BlockDepth != 1)
            {
                Log.LogError(new LogEntry(
                    "unexpected end-of-file", 
                    "encountered the end-of-file marker " +
                    "before all blocks were closed.", 
                    new SourceLocation(Code, index)));
                return new ReturnStatement();
            }

            State.Append(new ReturnStatement());

            return State.PopBlock();
        }
    }
}

