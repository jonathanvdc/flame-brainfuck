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

Let's compile these two statements and initialize the `BrainfuckState`. Both activities are highly related, so we might as well combine them in a single method. I've placed the function below &ndash; and all other code in the next few sections &ndash; in a `public static class` called `Brainfuck`.

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

As a concession to reality, we won't be creating an infinitely large `byte` array. Instead, the call to `new NewArrayExpression` creates a finite array with `ArraySize` elements of type `ElementType`. For now, let's assume that `ElementType` is `byte`.

## Compiling Brainfuck code: the high-level view

Next up, let's handle the actual Brainfuck code. For this, we'll write a function `IStatement Compile(ISourceDocument, BrainfuckState, ICompilerLog)`.

> We've already encountered `IStatement`, `BrainfuckState` and `ICompilerLog` by now; but `ISourceDocument` is new. An `ISourceDocument` is basically a file of source code whose source code can be retrieved as a string by accessing `ISourceDocument.Source`. Source documents are also useful for diagnostics: appending a `new SourceLocation(sourceDocument, index, length)` to a `new LogEntry` constructor call's argument list will associate the log entry with a particular location in the source code: specifically, the range of code that is `length` characters long and starts at offset `index` in the `sourceDocument`.

> The command-line interface visualizes source locations as a pair of line and column numbers, along with a caret-highlighted string of source code. For example, consider the following snippet of code.

> ```cs
Log.LogError(new LogEntry(
    "unexpected character",
    "the given left bracket character (']') " +
    "doesn't have a matching right bracket ('[') " +
    "to precede it.",
    new SourceLocation(Code, Index, 1)));
```

> This error message might be rendered like so:

> ```
mirror.bf:1:3: error: unexpected character: the given left bracket character (']') doesn't have a matching right bracket ('[') to precede it.
    ,.][,.]
      ^    
```

The `Compile` function consists of a simple loop over the source code. The nitty-gritty details of building Flame IR have been shoved into specialized functions, to give us a better overview of the high-level logic.

```cs
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
```

The general idea behind the `Compile` function is that every character in the source document is first handled individually, followed by a basic integrity check. We also append a `return;` statement, because failing to do so might give us an invalid method body.

## Generating IR for specific Brainfuck constructs

As noted before, `Compile` relies on a bunch of helper functions to generate IR. These functions are: `IntegerLiteral`, `Increment`, `Decrement`, `Print`, `Read` and `PopLoop`. Let's go over them one by one.

### Constructing integer literals

We've already created an integer literal before during this tutorial: the `Initialize` function uses one in the following statement.

```cs
// index = 0;
state.Append(
    indexVar.CreateSetStatement(
        new IntegerExpression(0)));
```

It's important to keep in mind that `new IntegerExpression(0)` produces a _32-bit signed integer literal,_ because C# (usually) interprets `0` as a 32-bit signed integer. Hence, the `new IntegerExpression(int)` constructor is called. Also, recall that our data consists of an array of bytes, i.e., _8-bit unsigned integers._ So we can't spell, say, `new IntegerExpression(1)` and then use that to represent the `1` in `array[index] = array[index] + 1;` &ndash; that'd amount to adding a 32-bit signed integer to a 8-bit unsigned integer, which breaks Flame IR's typing rules.

> The CLR back-end will probably accept adding a 32-bit signed integer to an 8-bit integer, because the IL code it generates uses only 32-bit integers on the stack. But that's just an implementation detail; other back-ends are allowed to compile ill-defined operations however they want. And if the CLR back-end ever decides to enforce the typing rules, then it too will ill-defined operations.

The easiest way to get a Flame IR literal for a given integer type is to first create a simple 32-bit signed integer literal, and then cast that to whatever integer type you want. The `IntegerLiteral` function does just that.

```cs
private static IExpression IntegerLiteral(
    int Value, IType Type)
{
    // (Type)Value;
    return new StaticCastExpression(
        new IntegerExpression(Value),
        Type)
        .Simplify();
}
```

There are two things here that I'd like to point out:
  * We're building a `StaticCastExpression`. Flame IR has many different types of casts. Static casts convert between primitive (built-in) types, such as integer and floating-point types.
  * After building the `StaticCastExpression`, we also call `Simplify()` on it, which will cause the `StaticCastExpression` to realize that its operand is an integer literal. The value returned by the call to `Simplify()` is an integer literal that has the value and type we want. We didn't have to call `Simplify()`, but it avoids a potential run-time cast.

> Side note: conversions can be represented as one of the following constructs in Flame IR.
>   * A static cast (`StaticCastExpression`), which we've just used.
>   * A dynamic cast (`DynamicCastExpression`), which tests if a reference/pointer conforms to a type, and converts it to a reference/pointer to said type.
>   * A reinterpret cast (`ReinterpretCastExpression`), which is like a `DynamicCastExpression` except that casting a reference/pointer to a type to which it does not conform results in undefined behavior. This allows it to omit the conformity check. This is useful for upcasts, and certain optimizations will replace dynamic casts with reinterpret casts if they can prove that a reference/pointer will always conform to some type.
>   * An as-instance expression (`AsInstanceExpression`), which is like `DynamicCastExpression`, except that it yields a `null` value instead of throwing an exception when its operand does not conform to the target type.
>   * An is-instance expression (`IsInstanceExpression`), which tests if a reference/pointer conforms to a type, and then yields that result as a Boolean value.
>   * A method call, which is used for user-defined expressions.

### Incrementing and decrementing variables

This part is pretty easy, and requires little explanation. `Increment` and `Decrement` load a variable, add or subtract `1`, and store that in the variable.

```cs
private static IStatement Increment(
    IVariable Variable)
{
    // Variable = Variable + (decltype(Variable))1;
    return Variable.CreateSetStatement(
        new AddExpression(
            Variable.CreateGetExpression(),
            IntegerLiteral(1, Variable.Type)));
}

private static IStatement Decrement(
    IVariable Variable)
{
    // Variable = Variable - (decltype(Variable))1;
    return Variable.CreateSetStatement(
        new SubtractExpression(
            Variable.CreateGetExpression(),
            IntegerLiteral(1, Variable.Type)));
}
```

### Writing characters to the output stream

The `Print` function writes `array[index]` to standard output, _as a character._ It's similar to the call to `WriteLine` we generated back when we generated "hello world" programs. The only catch is that `array[index]` is a `byte`, but `void Console.Write(char)` accepts a single `char` argument. So we'll need to insert a conversion. `StaticCastExpression` to the rescue!

```cs
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
```  

### Reading characters from the input stream

The `Read` function reads values from standard input, and writes them to `array[index]`. This is arguably the most complicated helper function, because `int Console.Read()` returns a negative value when the input stream is empty. However, Brainfuck expects a value of zero when this type of thing happens. We'll need to insert logic that translates subzero return values to zeros. `Read`'s implementation looks like this:

```cs
private static IStatement Read(BrainfuckState State)
{
    // var tmp = Read();
    // array[index] = tmp > 0 ? (ElementType)tmp : 0;
    var tmpVar = new LocalVariable("tmp", State.ReadMethod.ReturnType);
    return new BlockStatement(new IStatement[]
    {
        // tmp = Read();
        tmpVar.CreateSetStatement(
            new InvocationExpression(
                State.ReadMethod, null,
                new IExpression[] { })),
        // array[index] = tmp > 0 ? (ElementType)tmp : 0;
        State.ElementVariable.CreateSetStatement(
            new SelectExpression(
                new GreaterThanExpression(
                    tmpVar.CreateGetExpression(),
                    IntegerLiteral(0, tmpVar.Type)),
                new StaticCastExpression(
                    tmpVar.CreateGetExpression(),
                    State.ElementVariable.Type),
                IntegerLiteral(0, State.ElementVariable.Type)))
    });
}
```

This may seem daunting at first, but there's no super complicated logic here. It may help to sort of just stare at it for a while, and read it top-down until you get what's going on. The `new SelectExpression` call corresponds to the ternary operator in the comments.

### Wrapping blocks in `while` loops.

Remember how we're using a `Stack<List<IStatement>>` to handle Brainfuck's brackets ('[' and ']')? Well, `PopLoop` is where we pop values from that stack; it's called whenever we encounter a right bracket (']'). The idea is to first check that the stack contains more than one block &ndash; the bottom-of-stack block is off-limits because it corresponds to the function body, and was not created by a left bracket ('['). If that there is more than one block on the stack, then we'll pop the top-of-stack block, and wrap it in a `while` loop. If there is only one block on the stack, then we'll issue an error and return an empty statement as a placeholder.

```cs
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
            new SourceLocation(Code, Index, 1)));

        return EmptyStatement.Instance;
    }

    // while (array[index] != 0) { ... }
    return new WhileStatement(
        new InequalityExpression(
            State.ElementVariable.CreateGetExpression(),
            IntegerLiteral(0, State.ElementVariable.Type)),
        State.PopBlock());
}
```

## Can we compile Brainfuck now?

Actually, we still have to do a tiny bit of bookkeeping. Specifically, we need to:
  1. load the source code of the input file,
  2. resolve `System.Console`,
  3. resolve `void System.Console.Write(char)`,
  4. resolve `int System.Console.Read()`, and
  5. call `Brainfuck.Initialize` and `Brainfuck.Compile`.

To accomplish this, we'll rewrite method `GetMainBody` in `BrainfuckHandler` and replace it with the code below.

```cs
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
```

Most of this is just going through the motions, so I won't elaborate on that here. The first and last statements are interesting, though:

  * The first statement fetches the source code.

    ```cs
    var code = ProjectHandlerHelpers.GetSourceSafe(
        Project.GetSourceItems().Single(), Parameters);
    ```

    `ProjectHandlerHelpers.GetSourceSafe` will retrieve the source code for the source item we give it &ndash; that is, `Project.GetSourceItems().Single()` &ndash; and return the result as an `ISourceDocument`. If something goes wrong during this process, then an error is logged and `null` is returned instead.

  * The last statement initializes the `BrainfuckState` with a `byte` array that is ten-thousand elements long, compiles the Brainfuck source document to a statement, which is then returned.

  ```cs
  return Brainfuck.Compile(
      code,
      Brainfuck.Initialize(
          writeMethod, readMethod,
          PrimitiveTypes.UInt8, 10000),
      Parameters.Log);
  ```

Compile your compiler, and run it on an example file. You're welcome to use [`mirror.bf`](https://github.com/jonathanvdc/flame-brainfuck/blob/master/tests/mirror/mirror.bf), which just repeats whatever you throw at it. For example, you can do this:

```
$ ./flame-brainfuck.exe ./tests/mirror/mirror.bf -platform clr
$ echo "hi there" | ./tests/mirror/bin/mirror.exe
hi there
```

Congratulations! You are now the (proud) author of a working Brainfuck compiler! Good job! For the complete source code, see [the `flame-brainfuck` GitHub page](https://github.com/jonathanvdc/flame-brainfuck).
