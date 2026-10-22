using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Runtime.Benchmarks;

public class StoreBenchmarks
{
    private IStateStoreRegistry registry = null!;
    private StateStoreDefinition<Reference<int>> definition = null!;
    [GlobalSetup]
    public void Setup()
    {
        registry = StateStores.CreateRegistry();
        definition = StateStores.Define("counter", () => Reactive.Reference(0));
        definition.Use(registry);
    }
    [GlobalCleanup] public void Cleanup() => registry.Dispose();
    [Benchmark] public Reference<int> ResolveExisting() => definition.Use(registry);
    [Benchmark]
    public int RemoveAndRecreate()
    {
        registry.Remove(definition);
        return definition.Use(registry).Value;
    }
}