using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Viu.ServerRenderer.Benchmarks;

public class SerializationBenchmarks
{
    private ElementNode tree = null!;
    [Params(1, 100)] public int Elements { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        var children = new VirtualNode[Elements];
        for (int index = 0; index < Elements; index++)
        {
            children[index] = new ElementNode(new QualifiedName("p"), children: [new TextNode("<safe> & text")]);
        }

        tree = new ElementNode(new QualifiedName("main"), children: children);
    }
    [Benchmark] public Task<string> Render() => ServerRender.RenderToStringAsync(tree);
}
