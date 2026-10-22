using System.Linq;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Syntax.Css;

namespace Assimalign.Viu.Syntax.Benchmarks;

public class CssBenchmarks
{
    private string source = "";
    private CssStylesheetNode stylesheet = null!;
    private readonly CssSyntaxParser parser = new();
    [Params(1, 100)] public int Rules { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        source = string.Concat(Enumerable.Repeat(".card:hover > span { color: red; padding: 1rem; }\n", Rules));
        stylesheet = (CssStylesheetNode)parser.ParseSyntax(source).Nodes[0];
    }
    [Benchmark] public object Parse() => parser.ParseSyntax(source);
    [Benchmark] public string Serialize() => CssStylesheetWriter.Write(stylesheet);
    [Benchmark] public string Scope() => CssScopedRewriter.Rewrite(stylesheet, "data-v-benchmark");
}
