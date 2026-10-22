using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.FileRouting;
using Assimalign.Viu.Router;
using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.Utilities.Benchmarks;

public class UtilityBenchmarks
{
    private readonly FileRouteDescriptor[] pages = [new("/", "home", "Home"), new("/people/:id", "person", "Person", true)];
    [Params("hover:bg-red-500", "md:hover:[&>span]:bg-[#123456]/50")] public string Candidate { get; set; } = "";
    [Benchmark] public object ParseCandidate() => UtilityCandidateParser.Parse(Candidate);
    [Benchmark] public IReadOnlyList<RouteRecord> CreateFileRoutes() => FileRoutes.Create(pages);
}
