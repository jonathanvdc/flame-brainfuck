using System;
using System.Collections.Generic;
using Flame.Compiler;
using Flame.Compiler.Statements;
using Flame.Compiler.Variables;

namespace Flame.Brainfuck
{
    public class BrainfuckState
    {
        public BrainfuckState(
            IMethod PrintMethod, IMethod ReadMethod,
            IVariable ArrayVariable, IVariable IndexVariable)
        {
            this.PrintMethod = PrintMethod;
            this.ReadMethod = ReadMethod;
            this.ArrayVariable = ArrayVariable;
            this.IndexVariable = IndexVariable;
            this.blocks = new Stack<List<IStatement>>();
        }

        // A method that prints characters. 
        public IMethod PrintMethod { get; private set; }

        // A method that reads characters.
        public IMethod ReadMethod { get; private set; }

        // A variable that contains the data array.
        public IVariable ArrayVariable { get; private set; }

        // A variable that indexes the data array.
        public IVariable IndexVariable { get; private set; }

        // A variable that represents the currently indexed 
        // element in the data array.
        public IVariable ElementVariable
        {
            get 
            { 
                return new ElementVariable(
                    ArrayVariable.CreateGetExpression(), 
                    new IExpression[] 
                    { 
                        IndexVariable.CreateGetExpression() 
                    });
            }
        }

        private Stack<List<IStatement>> blocks;

        // Gets the depth of the block stack.
        public int BlockDepth { get { return blocks.Count; } }

        // Appends a statement to the top-of-stack block.
        public void Append(IStatement Statement)
        {
            blocks.Peek().Add(Statement);
        }

        // Pushes an empty block onto the stack of blocks.
        public void PushBlock()
        {
            blocks.Push(new List<IStatement>());
        }

        // Pops the top-of-stack block from the stack of blocks, 
        // and returns it as a statement. 
        public IStatement PopBlock()
        {
            return new BlockStatement(blocks.Pop());
        }
    }
}

