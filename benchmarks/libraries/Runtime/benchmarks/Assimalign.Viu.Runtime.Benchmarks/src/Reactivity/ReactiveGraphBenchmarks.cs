using System;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Runtime.Benchmarks;

public class ReactiveGraphBenchmarks
{
    private EffectScope scope = null!;
    private Reference<int> source = null!;
    private Computed<int> derived = null!;
    private int sink;

    [Params(1, 100)] public int Subscribers { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        scope = Reactive.EffectScope();
        scope.Run(() =>
        {
            source = Reactive.Reference(0);
            derived = Reactive.Computed(() => source.Value * 2);
            for (int index = 0; index < Subscribers; index++)
            {
                Reactive.Effect(() => sink = source.Value);
            }
        });
    }
    [GlobalCleanup] public void Cleanup() => scope.Stop();
    [Benchmark] public int Trigger() { source.Value++; return sink; }
    [Benchmark] public int CachedComputed() => derived.Value;
    [Benchmark] public int InvalidateComputed() { source.Value++; return derived.Value; }
    [Benchmark] public int Batch() { using (Reactive.Batch()) { for (int index = 0; index < 20; index++) { source.Value++; } } return sink; }
}
