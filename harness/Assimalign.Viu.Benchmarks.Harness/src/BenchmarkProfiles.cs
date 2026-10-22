using System;
using System.IO;
using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Toolchains.NativeAot;

namespace Assimalign.Viu.Benchmarks;

/// <summary>
/// The run profiles. A profile picks the measurement job; every profile measures allocations and writes the
/// GitHub markdown table plus the full JSON report that CI turns into history.
/// </summary>
/// <remarks>
/// <para>
/// Suites run <b>in-process</b> by default (BenchmarkDotNet's <see cref="InProcessEmitToolchain"/>: the same
/// emitted measurement harness, in the already-built benchmark process), which skips a rebuild of every suite in a
/// generated child project and keeps CI short.
/// </para>
/// <para>
/// <c>--out-of-process</c> restores BenchmarkDotNet's default toolchain (for example to compare GC modes in
/// separate processes); the measured viu package version is then forwarded to the child build.
/// </para>
/// </remarks>
public static class BenchmarkProfiles
{
    /// <summary>BenchmarkDotNet's default job: as many iterations as it takes to be stable. For investigations.</summary>
    public const string Local = "local";

    /// <summary>
    /// A fixed, bounded job for CI history: one launch, 5 warmups, 20 measured iterations. Fixed counts keep run
    /// time predictable and make consecutive runs comparable.
    /// </summary>
    public const string Ci = "ci";

    /// <summary>One invocation per benchmark: proves every benchmark builds and runs. Numbers are meaningless.</summary>
    public const string Dry = "dry";

    /// <summary>Creates the configuration for <paramref name="profile"/>.</summary>
    /// <param name="profile">One of <see cref="Local"/>, <see cref="Ci"/>, <see cref="Dry"/>.</param>
    /// <param name="assembly">The benchmark assembly.</param>
    /// <param name="addProfileJob">False when the command line already chose a job (<c>--job</c>, <c>--inProcess</c>).</param>
    /// <param name="outOfProcess">True to rebuild and run the suite in a child process (BenchmarkDotNet's default toolchain).</param>
    /// <returns>The configuration.</returns>
    public static IConfig CreateConfig(
        string profile,
        Assembly assembly,
        bool addProfileJob = true,
        bool outOfProcess = false,
        string? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assembly);

        string resolved = BenchmarkRuntimes.Normalize(runtime);

        // Each runtime writes to its own artifacts folder, so a JIT run and an AOT run of the same suite on one
        // machine do not overwrite each other's reports -- CI uploads both from the same checkout.
        string artifacts = Path.Combine(
            BenchmarkEnvironment.GetBenchmarksRoot(assembly),
            ".artifacts",
            "results",
            resolved == BenchmarkRuntimes.Jit ? assembly.GetName().Name! : $"{assembly.GetName().Name}.{resolved}");

        ManualConfig config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(artifacts)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full)

            .WithOptions(ConfigOptions.DisableLogFile);

        // An AOT run measures only the cases that opted in; see BenchmarkRuntimes for why it cannot measure them all.
        if (resolved == BenchmarkRuntimes.Aot)
        {
            config.AddFilter(new AnyCategoriesFilter([BenchmarkRuntimes.Category]));
        }

        if (addProfileJob)
        {
            config.AddJob(CreateJob(profile, assembly, outOfProcess, resolved));
        }

        return config;
    }

    /// <summary>Creates the job for <paramref name="profile"/>.</summary>
    /// <param name="profile">The profile name.</param>
    /// <param name="assembly">The benchmark assembly.</param>
    /// <param name="outOfProcess">True to use BenchmarkDotNet's default (child process) toolchain.</param>
    /// <returns>The job.</returns>
    public static Job CreateJob(string profile, Assembly assembly, bool outOfProcess = false, string? runtime = null)
    {
        Job job = profile.ToLowerInvariant() switch
        {
            Local => Job.Default,
            Ci => Job.Default.WithLaunchCount(1).WithWarmupCount(5).WithIterationCount(20),
            Dry => Job.Dry,
            _ => throw new ArgumentException($"Unknown benchmark profile '{profile}'. Use {Local}, {Ci} or {Dry}.", nameof(profile)),
        };

        string resolved = BenchmarkRuntimes.Normalize(runtime);
        job = job.WithId(resolved == BenchmarkRuntimes.Jit ? profile.ToLowerInvariant() : $"{profile.ToLowerInvariant()}-{resolved}");

        // The child build has to restore the measured viu build, not the pinned default. Both out-of-process
        // paths need it; NativeAOT additionally needs the ILCompiler package, which the suite csproj carries.
        MsBuildArgument version = new($"/p:ViuVersion={BenchmarkEnvironment.GetViuVersion(assembly)}");

        if (resolved == BenchmarkRuntimes.Aot)
        {
            // NativeAOT is always a child process: there is nothing to emit into in a natively compiled image.
            return job.WithToolchain(NativeAotToolchain.CreateBuilder().UseNuGet().ToToolchain()).WithArguments([version, new MsBuildArgument("/p:UseAppHost=false")]);
        }

        if (!outOfProcess)
        {
            return job.WithToolchain(InProcessEmitToolchain.Instance);
        }

        return job.WithArguments([version, new MsBuildArgument("/p:UseAppHost=false")]);
    }
}
