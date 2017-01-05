
Building a Brainfuck compiler with Flame
========================================

## Hello world for cool kids

Most programming language tutorials invariably begin by teaching us how to
create a program that prints "Hello World!" So I figure it's only fitting that
we, too, start by creating a program that does just that.

There's just one tiny catch, though: we won't be coding a "hello world" program
directly &ndash; because where's the fun in that, right? Instead, we'll teach
our "compiler" to accept Brainfuck files (extensions `*.b` and `*.bf`) and
then produce a "hello world" program regardless of what they contain.
That's a pretty hacky trick, which is exactly what makes it a fun thing to do.

First off, create a new class and call it `BrainfuckHandler`.
We'll configure it to handle any Brainfuck files we throw at it.

```cs
```
