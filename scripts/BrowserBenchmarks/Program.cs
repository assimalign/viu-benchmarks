using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

if (args.Contains("--install")) { return Microsoft.Playwright.Program.Main(["install", "chromium"]); }
string subject = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
int samples = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 20;
if (samples < 2) { throw new ArgumentOutOfRangeException(nameof(samples)); }
const string url = "http://127.0.0.1:5219";
var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();

await using var server = builder.Build();
server.Urls.Add(url);
using var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(subject);
Microsoft.AspNetCore.Builder.DefaultFilesExtensions.UseDefaultFiles(server, new Microsoft.AspNetCore.Builder.DefaultFilesOptions { FileProvider = files });
Microsoft.AspNetCore.Builder.StaticFileExtensions.UseStaticFiles(server, new Microsoft.AspNetCore.Builder.StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });
await server.StartAsync();
try
{
    using IPlaywright playwright = await Playwright.CreateAsync();
    await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
    IPage page = await browser.NewPageAsync();
    ConcurrentQueue<string> errors = new();
    page.PageError += (_, error) => { errors.Enqueue(error); Console.Error.WriteLine(error); };
    page.Console += (_, message) => { if (message.Type == "error" || message.Type == "warning") { Console.Error.WriteLine(message.Text); } };
    await page.GotoAsync(url);
    try { await page.Locator("#version").WaitForAsync(); } catch { Console.Error.WriteLine(await page.ContentAsync()); throw; }
    List<object> history = new();
    List<object> measurements = new();
    foreach (string scenario in new[] { "create", "create10000", "replace", "update", "select", "swap", "remove", "clear" })
    {
        List<double> durations = new();
        List<int> frames = new();
        for (int iteration = -3; iteration < samples; iteration++)
        {
            await Measure(page, "reset");
            if (scenario.StartsWith("create", StringComparison.Ordinal)) { await Measure(page, "clear"); }
            ILocator rows = page.Locator("tbody tr");
            IElementHandle? movedRow = scenario == "swap" ? await rows.Nth(1).ElementHandleAsync() : null;
            string? previousFirstIdentifier = scenario is "replace" or "remove" ? await rows.First.Locator("td").First.TextContentAsync() : null;
            JsonElement measured = await Measure(page, scenario);
            double elapsed = measured.GetProperty("elapsed").GetDouble();
            int crossings = measured.GetProperty("frames").GetInt32();
            if (crossings <= 0) { throw new InvalidOperationException("No command frames were counted."); }
            int expected = scenario == "clear" ? 0 : scenario == "remove" ? 999 : scenario == "create10000" ? 10000 : 1000;
            if (await page.Locator("tbody tr").CountAsync() != expected) { throw new InvalidOperationException(scenario + " produced the wrong row count."); }
            if (scenario == "update" && await rows.Filter(new() { HasText = "updated" }).CountAsync() != 100) { throw new InvalidOperationException("Partial update did not change exactly 100 labels."); }
            if (scenario == "select" && await page.Locator("tbody tr.selected").CountAsync() != 1) { throw new InvalidOperationException("Selection did not mark exactly one row."); }
            if (scenario == "swap" && (movedRow is null || !await movedRow.EvaluateAsync<bool>("row => row === document.querySelectorAll('tbody tr')[998]"))) { throw new InvalidOperationException("Swap did not preserve and move the keyed row."); }
            if (previousFirstIdentifier is not null && await rows.First.Locator("td").First.TextContentAsync() == previousFirstIdentifier) { throw new InvalidOperationException(scenario + " retained the old first row."); }
            if (movedRow is not null) { await movedRow.DisposeAsync(); }
            if (!errors.IsEmpty) { throw new InvalidOperationException(string.Join('\n', errors)); }
            if (iteration >= 0) { durations.Add(elapsed); frames.Add(crossings); }
        }
        double[] ordered = durations.Order().ToArray();
        double median = (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / 2;
        history.Add(new { name = "Browser.KeyedRows." + scenario, unit = "ms", value = median });
        measurements.Add(new { scenario, medianMilliseconds = median, samplesMilliseconds = durations, commandFrames = frames });
        Console.WriteLine($"{scenario}: {median:F3} ms ({samples} samples)");
    }
    Directory.CreateDirectory(output);
    await File.WriteAllTextAsync(Path.Combine(output, "time.json"), JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(Path.Combine(output, "browser-results.json"), JsonSerializer.Serialize(new { engine = "chromium", browser = browser.Version, runtime = "browser-wasm", warmups = 3, samples, measurements }, new JsonSerializerOptions { WriteIndented = true }));
}
finally { await server.StopAsync(); }
return 0;

static async Task<JsonElement> Measure(IPage page, string id) => await page.EvaluateAsync<JsonElement>("""
    id => new Promise((resolve, reject) => {
        const version = document.querySelector('#version');
        const previous = version.textContent;
        const frames = window.benchmarkCounters.frames;
        const start = performance.now();
        const observer = new MutationObserver(() => {
            if (version.textContent !== previous) {
                observer.disconnect(); clearTimeout(timeout); resolve({ elapsed: performance.now() - start, frames: window.benchmarkCounters.frames - frames });
            }
        });
        const timeout = setTimeout(() => { observer.disconnect(); reject(new Error('Render acknowledgement timed out')); }, 10000);
        observer.observe(version, { childList: true, characterData: true, subtree: true });
        document.getElementById(id).click();
    })
    """, id);
