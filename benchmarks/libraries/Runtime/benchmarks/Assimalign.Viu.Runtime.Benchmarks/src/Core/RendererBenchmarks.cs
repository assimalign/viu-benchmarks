using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Testing.Benchmarks;

namespace Assimalign.Viu.Runtime.Benchmarks;

public class RendererBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public InteropScenario Scenario { get; set; } = null!;
    public IEnumerable<InteropScenario> Scenarios() => InteropScenarioLibrary.Discover();
    [Benchmark] public int PatchScenario() => Scenario.Run(ScenarioVariant.Optimized);
}