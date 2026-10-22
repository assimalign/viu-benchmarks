using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.Router.Benchmarks;

public class RouteMatcherBenchmarks
{
    private RouteMatcher matcher = null!;
    [Params(10, 100)] public int Routes { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        var records = new RouteRecord[Routes + 1];
        for (int index = 0; index < Routes; index++)
        {
            records[index] = new RouteRecord("/section-" + index, name: "page-" + index);
        }

        records[Routes] = new RouteRecord("/people/:id", name: "person");
        matcher = new RouteMatcher(records);
    }
    [Benchmark] public RouteLocation StaticRoute() => matcher.Resolve("/section-9");
    [Benchmark] public RouteLocation DynamicRoute() => matcher.Resolve("/people/42?tab=profile#details");
    [Benchmark] public RouteLocation MissingRoute() => matcher.Resolve("/does-not-exist");
}
