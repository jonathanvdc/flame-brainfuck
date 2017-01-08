# Compiling Brainfuck code

Most of the groundwork is done now, and the "hello world" compiler is in ideal shape for us to turn it into an actual Brainfuck compiler.

## Brainfuck operators

It's worth reviewing how Brainfuck code can be translated to, say, C# before we actually implement a similar translation. I ~~stole~~ borrowed the table below from [the Wikipedia article on Brainfuck](https://en.wikipedia.org/wiki/Brainfuck), and then added the rightmost column myself.

| Brainfuck command | C equivalent | C# equivalent |
| ------------------ | ------------- | ----------- |
| (Program Start) | `char array[infinitely large size] = {0}; char *ptr=array;` | `var array = new byte[infinitely large size]; int index = 0;` |
| `>` | `++ptr;` | `++index;` |
| `<` | `--ptr;` | `--index;` |
| `+` | `++*ptr;` | `++array[index];` |
| `-` | `--*ptr;` | `--array[index];` |
| `.` | `putchar(*ptr);` | `Console.Write((char)array[index]);` |
| `,` | `*ptr=getchar();` | `int r = Console.Read(); array[index] = r > 0 ? (byte)r : 0;` |
| `[` | `while (*ptr) {` | `while (array[index] != 0) {` |
| `]` | `}` | `}` |

Building a source-to-source Brainfuck compiler is actually fairly trivial: you iterate over all characters in the source document. For each character, look up its C/C# equivalent, and replace it with that equivalent statement if there is such a statement. Otherwise, skip the character.

So `>+[-]<` is translated to

```cs
var array = new byte[infinitely large size];
int index = 0;

++index;
++array[index];
while (array[index] != 0) {
    --array[index];
}
--index;
```

We won't actually be translating to C or C# &ndash; we'll be translating to Flame IR. But the same concept applies: we iterate over the input, and emit a statement for every meaningful input character.

Also, there's one catch: the `[` and `]` operators are special because their C/C# equivalents aren't truly statements at all. A `while` statement is a statement, and a block statement is a statement, but `while (array[index] != 0) {` is _not_ a statement. It's a line of code.

If we were simply translating Brainfuck to C#, then we'd simply emit lines of code. But IR has no concept of what a "line of IR" is. There are only statements and expressions. When we emit a `while` statement, we need to know what all of its child statements are.

What we need is a _stack_ (of lists of statements), that is, a `Stack<List<IStatement>>`. We initially push one `List<IStatement>` onto the stack. When we encounter any meaningful character `c`, we do the following:
  * If `c == '['` then we'll push an empty `List<IStatement>` onto the stack.
  * If `c == ']'` then we'll pop a `List<IStatement>` from the stack, wrap it in a `while` block, and append it to the new top-of-stack `List<IStatement>`.
  * In any other case we'll append a statement to the top-of-stack list.

When all characters have been processed, then we'll pop the top-of-stack list of statements, turn that into a block statement, and return it.

We might also get mismatched brackets (e.g., `[[+]`), which we should report with a diagnostic. But they're sort of a special case, so I didn't include them in the logic above.

## A state data structure for Brainfuck

This may not be entirely obvious, but we'll be juggling quite a bit of state in our Brainfuck compiler. We have the mutable `Stack<List<IStatement>>` described above, but we also have four _immutable_ quantities that we'd like to keep handy:

  * The `IMethod` instance that represents `static void System.Console.Write(char)`, so we can emit calls that print characters to the command-line. This is similar to how we used an `IMethod` instance for `System.Console.WriteLine` to emit a call that prints `"Hello, world!"`.
  * The `IMethod` instance for `static int System.Console.Read()`, so we can emit calls that procure input.
  * A handle to the `array` variable. This is encoded as a `IVariable` instance in Flame.
  * A handle to the `index` variable.

> `IVariable`s do not occur directly in Flame IR, which consists exclusively of expressions (`IExpression`) and statements (`IStatement`). Instead, they can be used to build expressions and statements that set the variables they represent using `IExpression CreateGetExpression()` and `IStatement CreateSetStatement(IExpression Value)`.

Additionally, we'd also like to store a _derived_ value: a variable that represents `array[index]`. We could build this variable every time that it's requested, but it's probably more efficient to construct it once and then save it.

To make our lives easier, we can put all of this information in a single data structure, pass it around and call it a day. Here's the source code for the `BrainfuckState` data structure. It should all be pretty straightforward.

```cs
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
        this.ElementVariable = new ElementVariable(
            ArrayVariable.CreateGetExpression(),
            new IExpression[]
            {
                IndexVariable.CreateGetExpression()
            });
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
    public IVariable ElementVariable { get; private set; }

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
```

Maybe the one thing that is worth reviewing in the code above is the last constructor statement.

```cs
this.ElementVariable = new ElementVariable(
    ArrayVariable.CreateGetExpression(),
    new IExpression[]
    {
        IndexVariable.CreateGetExpression()
    });
```

It represents `array[index]` by first loading `array`'s value, and then indexing that with `index`'s value. The result is an `ElementVariable`, which is an `IVariable`.

> I'm pointing this out because I'd like to make clear that not all variables incur allocation: `array` and `index` are variables that need to be stored somewhere, but `ElementVariable` is not. So a variable in Flame is _not_ a unique storage location: it's just some quantity that can be loaded or assigned a value.

## Initializing the `BrainfuckState`

Any Brainfuck program starts with the following two statements.

```cs
var array = new byte[infinitely large size];
int index = 0;
```

Let's compile these two statements and initialize the `BrainfuckState`. Both activities are highly related, so we might as well combine them in a single method.

```cs
public static BrainfuckState Initialize(
    IMethod PrintMethod, IMethod ReadMethod,
    IType ElementType, int ArraySize)
{
    // Create the 'array' variable, with type 'ElementType[]'.
    var arrayVar = new LocalVariable(
        "array", ElementType.MakeArrayType(1));

    // Create the 'index' variable, with type 'int'.
    var indexVar = new LocalVariable(
        "index", PrimitiveTypes.Int32);

    // Create the Brainfuck state.
    var state = new BrainfuckState(
        PrintMethod, ReadMethod, arrayVar, indexVar);

    // Push a single block onto the stack.
    state.PushBlock();

    // index = 0;
    state.Append(
        indexVar.CreateSetStatement(
            new IntegerExpression(0)));

    // array = new ElementType[ArraySize];
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
```
