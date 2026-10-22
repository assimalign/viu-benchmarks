using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Browser;

namespace Assimalign.Viu.Browser.Benchmarks;

// Browser-only interop is measured by the separate browser lane, not simulated as CoreCLR calls.
public class BrowserContractBenchmarks
{
    [Benchmark] public object TransitionContract() => Transition.Registration.Contract;
    [Benchmark] public object TransitionGroupContract() => TransitionGroup.Registration.Contract;
}
