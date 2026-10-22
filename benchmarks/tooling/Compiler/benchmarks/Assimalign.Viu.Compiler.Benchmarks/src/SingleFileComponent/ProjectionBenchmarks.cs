using System.Linq;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.Compiler.Benchmarks;

public class ProjectionBenchmarks
{
    private SingleFileComponentProjectionInput input;
    [Params(1, 100)] public int Elements { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        const string path = "C:/benchmarks/Example.viu";
        string source = "<template><div>" + string.Concat(Enumerable.Repeat("<span>{{ Count }}</span>", Elements)) +
            "</div></template>\n@script { public int Count { get; set; } }\n<style scoped>span { color:red; }</style>";
        input = new(SingleFileComponentFormat.Viu, path, "Example.viu", source, "Benchmark", "Example", "Example.g.cs",
            CssComponentHash.Resolve(path, "C:/benchmarks"), null, false);
    }
    [Benchmark] public object Project() => SingleFileComponentProjection.Project(input, default);
    [Benchmark] public string Emit() => SingleFileComponentSourceEmitter.Emit(SingleFileComponentProjection.Project(input, default).Model);
}
