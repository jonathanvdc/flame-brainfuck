# Compiling Brainfuck code

Most of the groundwork is done now, and the "hello world" compiler is in ideal shape for us to turn it into an actual Brainfuck compiler.

## Brainfuck operators

It's worth reviewing how Brainfuck code can be translated to, say, C# before we actually implement a similar translation. I ~~stole~~ borrowed the table below from [the Wikipedia article on Brainfuck](https://en.wikipedia.org/wiki/Brainfuck), and then added the rightmost column myself.

| Brainfuck command | C equivalent | C# equivalent |
| ------------------ | ------------- |
| (Program Start) | `char array[infinitely large size] = {0}; char *ptr=array;` | `var array = new byte[infinitely large size]; int index = 0;` |
| `>` | `++ptr;` | `++index;` |
| `<` | `--ptr;` | `--index;` |
| `+` | `++*ptr;` | `++array[index];` |
| `-` | `--*ptr;` | `--array[index];` |
| `.` | `putchar(*ptr);` | `Console.Write((char)array[index]);` |
| `,` | `*ptr=getchar();` | `int r = Console.Read(); array[index] = r > 0 ? (byte)r : 0;`
| `[` | `while (*ptr) {` | `while (array[index] != 0) {` |
| `]` | `}` | `}` |

Building a Brainfuck compiler is quite simple: you iterate over all characters in the source document. For each character, look up its C/C# equivalent, and replace it with that equivalent statement if there is such a statement. Otherwise, skip the character.

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
