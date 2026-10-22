using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Assimalign.Viu.Benchmarks;

/// <summary>
/// The entry point every <c>*.Benchmarks</c> project uses:
/// <code>public static int Main(string[] args) => BenchmarkProgram.Run(args, typeof(Program).Assembly);</code>
/// </summary>
/// <remarks>
/// <para>
/// Arguments pass through to BenchmarkDotNet (<c>--filter</c>, <c>--list flat</c>, <c>--join</c>,
/// <c>--inProcess</c>, ...), plus two of ours: <c>--profile local|ci|dry</c> (or the
/// <c>VIU_BENCHMARK_PROFILE</c> environment variable) and <c>--runtime jit|aot</c> (or <c>VIU_BENCHMARK_RUNTIME</c>) and <c>--out-of-process</c> (see
/// <see cref="BenchmarkProfiles"/>). With no filter the whole suite runs, so a
/// non-interactive run never stops at BenchmarkDotNet's selection prompt.
/// </para>
/// <para>
/// The process exits with 1 when any benchmark failed to build, threw, or failed validation, so CI fails on a
/// broken benchmark instead of publishing a partial result. It also exits with 1 when nothing ran (a filter that
/// matches no benchmark), unless the arguments only asked for a listing or help.
/// </para>
/// </remarks>
public static class BenchmarkProgram
{
    private static readonly string[] SelectionOptions =
        ["--filter", "-f", "--allCategories", "--anyCategories", "--list", "--info", "--help", "-h", "--version", "--attribute", "-a"];

    private static readonly string[] InformationOptions = ["--list", "--info", "--help", "-h", "--version"];

    /// <summary>Runs the benchmarks in <paramref name="assembly"/> under the requested profile.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="assembly">The benchmark assembly, normally <c>typeof(Program).Assembly</c>.</param>
    /// <returns>0 when every selected benchmark ran; 1 when one failed or none was selected.</returns>
    public static int Run(string[] args, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(assembly);

        List<string> arguments = [.. args];
        string profile = TakeOption(arguments, "--profile")
            ?? Environment.GetEnvironmentVariable("VIU_BENCHMARK_PROFILE")
            ?? BenchmarkProfiles.Local;

        if (!arguments.Any(argument => SelectionOptions.Contains(argument, StringComparer.OrdinalIgnoreCase)))
        {
            arguments.Add("--filter");
            arguments.Add("*");
        }

        bool outOfProcess = TakeFlag(arguments, "--out-of-process");
        string runtime = BenchmarkRuntimes.Normalize(
            TakeOption(arguments, "--runtime") ?? Environment.GetEnvironmentVariable("VIU_BENCHMARK_RUNTIME"));

        // In-process is already the default; BenchmarkDotNet's own --inProcess would replace the profile's job with its
        // default job, so it is absorbed here instead.
        TakeFlag(arguments, "--inProcess");
        TakeFlag(arguments, "-i");
        bool jobFromCommandLine = arguments.Any(argument => argument is "--job" or "-j" or "--runtimes" or "-r");
        IConfig config = BenchmarkProfiles.CreateConfig(profile, assembly, addProfileJob: !jobFromCommandLine, outOfProcess, runtime);

        string host = runtime == BenchmarkRuntimes.Aot
            ? $"nativeaot ({BenchmarkRuntimes.Category} cases only)"
            : outOfProcess ? "out-of-process" : "in-process";
        Console.WriteLine(
            $"// Viu benchmarks: profile={profile}, runtime={runtime}, {host}, "
            + $"tfm={RuntimeInformation.FrameworkDescription}, os={RuntimeInformation.OSDescription}, "
            + $"viu={BenchmarkEnvironment.GetViuVersion(assembly)}");

        Summary[] summaries = [.. BenchmarkSwitcher.FromAssembly(assembly).Run([.. arguments], config)];

        // A filter that matches nothing makes BenchmarkDotNet print suggestions and return no summaries; a run that
        // measured nothing must not pass as a success.
        // An AOT run filters to the opt-in category, and most suites have no case in it. That is the designed
        // outcome, not a broken filter, so it reports nothing and succeeds rather than failing the run.
        if (summaries.All(summary => summary.Reports.Length == 0) && runtime == BenchmarkRuntimes.Aot)
        {
            Console.WriteLine($"// No benchmark in this suite is marked [BenchmarkCategory(\"{BenchmarkRuntimes.Category}\")]; nothing to measure on {runtime}.");
            return 0;
        }

        if (summaries.All(summary => summary.Reports.Length == 0)
            && !arguments.Any(argument => InformationOptions.Contains(argument, StringComparer.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"// No benchmark ran. The arguments selected none: {string.Join(' ', arguments)}");
            return 1;
        }

        return summaries.Any(Failed) ? 1 : 0;
    }

    private static bool Failed(Summary summary)
        => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success);

    private static bool TakeFlag(List<string> arguments, string name)
        => arguments.RemoveAll(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) > 0;

    private static string? TakeOption(List<string> arguments, string name)
    {
        int index = arguments.FindIndex(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index == arguments.Count - 1)
        {
            throw new ArgumentException($"{name} needs a value.");
        }

        string value = arguments[index + 1];
        arguments.RemoveRange(index, 2);
        return value;
    }
}
