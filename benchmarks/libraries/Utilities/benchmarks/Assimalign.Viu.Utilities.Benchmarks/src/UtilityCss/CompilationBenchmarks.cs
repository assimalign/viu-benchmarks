using System.Linq;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.Utilities.Benchmarks;

public class CompilationBenchmarks
{
    private string[] candidates = [];
    [Params(10, 1000)] public int Candidates { get; set; }
    [GlobalSetup] public void Setup() => candidates = Enumerable.Range(1, Candidates).Select(index => $"hover:p-[{index}px]").ToArray();
    [Benchmark] public UtilityCssCompilationResult Compile() => UtilityCssCompiler.Compile(candidates);
}