using Assimalign.Viu.Benchmarks;

namespace Assimalign.Viu.Compiler.Benchmarks;

public static class Program
{
    public static int Main(string[] arguments) => BenchmarkProgram.Run(arguments, typeof(Program).Assembly);
}
