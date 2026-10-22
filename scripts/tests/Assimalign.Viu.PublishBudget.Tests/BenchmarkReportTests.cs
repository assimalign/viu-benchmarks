using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.PublishBudget.Tests;

public sealed class BenchmarkReportTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "viu-reports-" + Guid.NewGuid().ToString("N"));
    private static string Scripts => FindScripts();

    [Fact]
    public void Compare_RemovedCase_FailsInsteadOfSilentlyDroppingIt()
    {
        string before = Report("before", "One", "Two");
        string after = Report("after", "One");
        Run("Compare-Benchmarks.ps1", "-Baseline", before, "-Current", after, "-FailOnRegression").ExitCode.ShouldBe(1);
    }
    [Fact]
    public void Compare_AllocatedBytesIncrease_FailsEvenWhenTimeIsUnchanged()
    {
        string before = Report("before", "One");
        string after = Report("after", "One");
        string file = Path.Combine(after, "test-report-full.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"BytesAllocatedPerOperation\":10", "\"BytesAllocatedPerOperation\":20"));
        Run("Compare-Benchmarks.ps1", "-Baseline", before, "-Current", after, "-FailOnRegression").ExitCode.ShouldBe(1);
    }
    [Fact]
    public void Compare_MissingCurrentAllocation_RejectsIncompleteMeasurement()
    {
        string before = Report("before", "One");
        string after = Report("after", "One");
        string file = Path.Combine(after, "test-report-full.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"BytesAllocatedPerOperation\":10", "\"BytesAllocatedPerOperation\":null"));
        Run("Compare-Benchmarks.ps1", "-Baseline", before, "-Current", after, "-FailOnRegression").ExitCode.ShouldNotBe(0);
    }
    [Fact]
    public void Compare_DifferentOperatingSystems_RejectsComparison()
    {
        string before = Report("before", "One");
        string after = Report("after", "One");
        File.WriteAllText(Path.Combine(before, "run.json"), "{\"os\":\"Windows\",\"architecture\":\"Arm64\",\"runtime\":\"jit\",\"profile\":\"ci\"}");
        File.WriteAllText(Path.Combine(after, "run.json"), "{\"os\":\"Linux\",\"architecture\":\"Arm64\",\"runtime\":\"jit\",\"profile\":\"ci\"}");
        Run("Compare-Benchmarks.ps1", "-Baseline", before, "-Current", after).ExitCode.ShouldNotBe(0);
    }
    [Fact]
    public void History_FailedCase_RejectsPartialHistoryPoint()
    {
        string source = Report("source", "One");
        string file = Path.Combine(source, "test-report-full.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("{\"Median\":100,\"Mean\":100,\"StandardDeviation\":1}", "null"));
        Run("ConvertTo-BenchmarkHistory.ps1", "-Path", source, "-TimeOutput", Path.Combine(temporary, "time.json"), "-AllocationOutput", Path.Combine(temporary, "alloc.json")).ExitCode.ShouldNotBe(0);
    }
    [Fact]
    public void History_SameTypeInDifferentNamespaces_KeepsBothIdentities()
    {
        string source = Report("source", "First", "Second");
        string output = Path.Combine(temporary, "time.json");
        Run("ConvertTo-BenchmarkHistory.ps1", "-Path", source, "-TimeOutput", output, "-AllocationOutput", Path.Combine(temporary, "alloc.json")).ExitCode.ShouldBe(0);
        using JsonDocument results = JsonDocument.Parse(File.ReadAllText(output));
        results.RootElement.GetArrayLength().ShouldBe(2);
        results.RootElement[0].GetProperty("name").GetString().ShouldBe("First.SameType.Work");
        results.RootElement[1].GetProperty("name").GetString().ShouldBe("Second.SameType.Work");
    }
    [Fact]
    public void BrowserFrames_ExcessiveCrossings_FailsTheExplicitCeiling()
    {
        Directory.CreateDirectory(temporary);
        string baseline = Path.Combine(temporary, "baseline.json");
        string results = Path.Combine(temporary, "browser.json");
        File.WriteAllText(baseline, """{"engine":"chromium","runtime":"browser-wasm","ceilings":{"update":2}}""");
        File.WriteAllText(results, """{"engine":"chromium","runtime":"browser-wasm","samples":2,"measurements":[{"scenario":"update","commandFrames":[2,101]}]}""");
        Run("Test-BrowserCommandFrames.ps1", "-ResultsPath", results, "-BaselinePath", baseline).ExitCode.ShouldNotBe(0);
    }
    [Fact]
    public void BrowserFrames_MissingScenario_FailsInsteadOfPublishingPartialResults()
    {
        Directory.CreateDirectory(temporary);
        string baseline = Path.Combine(temporary, "baseline.json");
        string results = Path.Combine(temporary, "browser.json");
        File.WriteAllText(baseline, """{"engine":"chromium","runtime":"browser-wasm","ceilings":{"create":2,"update":2}}""");
        File.WriteAllText(results, """{"engine":"chromium","runtime":"browser-wasm","samples":2,"measurements":[{"scenario":"create","commandFrames":[2,2]}]}""");
        Run("Test-BrowserCommandFrames.ps1", "-ResultsPath", results, "-BaselinePath", baseline).ExitCode.ShouldNotBe(0);
    }
    private string Report(string directory, params string[] namespaces)
    {
        string path = Path.Combine(temporary, directory);
        Directory.CreateDirectory(path);
        string[] cases = Array.ConvertAll(namespaces, name => "{\"Namespace\":\"" + name + "\",\"Type\":\"SameType\",\"MethodTitle\":\"Work\",\"Statistics\":{\"Median\":100,\"Mean\":100,\"StandardDeviation\":1},\"Memory\":{\"BytesAllocatedPerOperation\":10}}");
        File.WriteAllText(Path.Combine(path, "test-report-full.json"), "{\"Benchmarks\":[" + string.Join(',', cases) + "]}");
        return path;
    }
    private static (int ExitCode, string Output) Run(string script, params string[] arguments)
    {
        ProcessStartInfo start = new("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(Path.Combine(Scripts, script));
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30000).ShouldBeTrue();
        return (process.ExitCode, output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult());
    }
    private static string FindScripts()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "scripts", "Compare-Benchmarks.ps1");
            if (File.Exists(candidate)) { return Path.GetDirectoryName(candidate)!; }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Benchmark scripts not found.");
    }
    public void Dispose() { if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); } }
}