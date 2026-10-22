using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Assimalign.Viu.Benchmarks;

/// <summary>
/// Build-time facts a benchmark assembly carries (written by the repository's Directory.Build.props as
/// <see cref="AssemblyMetadataAttribute"/>): which viu build it measures and where this repository is.
/// </summary>
public static class BenchmarkEnvironment
{
    /// <summary>The viu package version the assembly was built against (a CI build such as 10.0.0-preview.0.ci.38).</summary>
    /// <param name="assembly">The benchmark assembly.</param>
    /// <returns>The version.</returns>
    public static string GetViuVersion(Assembly assembly) => GetMetadata(assembly, "ViuVersion");

    /// <summary>The root of this repository, with a trailing separator.</summary>
    /// <param name="assembly">The benchmark assembly.</param>
    /// <returns>The directory.</returns>
    public static string GetBenchmarksRoot(Assembly assembly) => GetMetadata(assembly, "BenchmarksRoot");

    /// <summary>Resolves <paramref name="relativePath"/> against the root of this repository.</summary>
    /// <param name="assembly">The benchmark assembly.</param>
    /// <param name="relativePath">A path relative to the repository root, with forward slashes.</param>
    /// <returns>The full path.</returns>
    public static string GetRepositoryPath(Assembly assembly, string relativePath)
        => Path.GetFullPath(Path.Combine(GetBenchmarksRoot(assembly), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string GetMetadata(Assembly assembly, string key)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value;

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} carries no '{key}' metadata. Benchmark projects must be named *.Benchmarks " +
                "(or set ViuAreaBenchmark=true) so the repository's Directory.Build.props wires them up.");
        }

        return value;
    }
}
