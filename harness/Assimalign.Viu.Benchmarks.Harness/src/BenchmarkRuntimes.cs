using System;

namespace Assimalign.Viu.Benchmarks;

/// <summary>
/// The runtime a suite is measured on: the regular JIT, or NativeAOT.
/// </summary>
/// <remarks>
/// <para>
/// This is a separate axis from the profile. A profile decides how hard a benchmark is measured; a runtime decides
/// what it is measured on. The two combine: the <c>ci</c> profile on the <c>aot</c> runtime is the recorded NativeAOT
/// series.
/// </para>
/// <para>
/// <b>NativeAOT is opt-in per benchmark, and has to be.</b> The JIT runtime measures in-process, so the whole suite
/// costs one build. NativeAOT cannot: BenchmarkDotNet generates and natively compiles a project per benchmark case,
/// which takes on the order of a minute each. Compiling all of this repository's cases would take the better part of
/// a day, so an AOT run measures only the cases that carry <see cref="Category"/>, and a suite with none measures
/// nothing and says so.
/// </para>
/// <para>
/// Mark a case with <c>[BenchmarkCategory(BenchmarkRuntimes.Category)]</c> when the question "is this different once
/// the JIT is gone" is a real one for it: reflection-free composition, generic dispatch, startup-shaped work, a path
/// whose cost depends on devirtualization. Marking everything defeats the point.
/// </para>
/// </remarks>
public static class BenchmarkRuntimes
{
    /// <summary>The regular JIT, measured in-process. The default, and what every unqualified series records.</summary>
    public const string Jit = "jit";

    /// <summary>NativeAOT, measured out of process in a natively compiled child. Only <see cref="Category"/> cases run.</summary>
    public const string Aot = "aot";

    /// <summary>
    /// The BenchmarkDotNet category that opts a benchmark into AOT runs. An <see cref="Aot"/> run filters to it.
    /// </summary>
    public const string Category = "Aot";

    /// <summary>Normalizes and validates a runtime name.</summary>
    /// <param name="runtime">The runtime name, case-insensitive; null or empty means <see cref="Jit"/>.</param>
    /// <returns><see cref="Jit"/> or <see cref="Aot"/>.</returns>
    /// <exception cref="ArgumentException">The name is neither.</exception>
    public static string Normalize(string? runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime))
        {
            return Jit;
        }

        return runtime.Trim().ToLowerInvariant() switch
        {
            Jit => Jit,
            Aot => Aot,
            _ => throw new ArgumentException($"Unknown benchmark runtime '{runtime}'. Use {Jit} or {Aot}.", nameof(runtime)),
        };
    }
}
