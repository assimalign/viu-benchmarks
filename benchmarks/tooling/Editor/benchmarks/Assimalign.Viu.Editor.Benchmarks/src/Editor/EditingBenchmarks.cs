using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Benchmarks;

namespace Assimalign.Viu.Editor.Benchmarks;

// Measures the packaged server's real request latency. Allocation reports cover the client transport,
// not the server process; this suite does not claim to measure Visual Studio paint latency.
public class EditingBenchmarks
{
    private const string Document = "file:///benchmarks/Example.viu";
    private Process server = null!;
    private Stream input = null!;
    private Stream output = null!;
    private string source = "";
    private int requestIdentifier;
    private int version;

    [Params(10, 500)] public int Elements { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        string executable = BenchmarkEnvironment.GetRepositoryPath(typeof(Program).Assembly,
            ".artifacts/subjects/editor/Assimalign.Viu.LanguageServer.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Run scripts/Prepare-EditorSubject.ps1 -Vsix <Viu.vsix> first.", executable);
        }

        server = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        server.ErrorDataReceived += (_, _) => { };
        server.BeginErrorReadLine();
        input = server.StandardInput.BaseStream;
        output = server.StandardOutput.BaseStream;
        Request("initialize", new JsonObject { ["capabilities"] = new JsonObject() });
        Notify("initialized", new JsonObject());
        var builder = new StringBuilder("<template>\n");
        for (int index = 0; index < Elements; index++)
        {
            builder.Append("<div>{{ Count }}</div>\n");
        }

        source = builder.Append("</template>\n@script {\npublic int Count { get; set; }\n}\n").ToString();
        Notify("textDocument/didOpen", new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = Document,
                ["languageId"] = "viu",
                ["version"] = ++version,
                ["text"] = source
            }
        });
        JsonNode? completions = Completion();
        if (completions is null || !completions.ToJsonString().Contains("Count", StringComparison.Ordinal))
        {
            Cleanup();
            throw new InvalidOperationException("The editor subject returned no Count completion: " + completions?.ToJsonString());
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (server is null)
        {
            return;
        }

        try { if (!server.HasExited) { Request("shutdown", null); Notify("exit", null); } }
        finally { if (!server.WaitForExit(5000)) { server.Kill(true); } server.Dispose(); }
    }

    [Benchmark]
    public JsonNode? Completion() => Request("textDocument/completion", new JsonObject
    {
        ["textDocument"] = new JsonObject { ["uri"] = Document },
        ["position"] = new JsonObject { ["line"] = 1, ["character"] = 13 }
    });
    [Benchmark] public JsonNode? SemanticTokens() => Request("textDocument/semanticTokens/full", new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = Document } });
    [Benchmark] public JsonNode? Folding() => Request("textDocument/foldingRange", new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = Document } });
    [Benchmark] public JsonNode? Symbols() => Request("textDocument/documentSymbol", new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = Document } });
    [Benchmark]
    public JsonNode? EditAndComplete()
    {
        Notify("textDocument/didChange", new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = Document, ["version"] = ++version },
            ["contentChanges"] = new JsonArray(new JsonObject { ["text"] = source + new string(' ', version % 2) })
        });
        return Completion();
    }

    private JsonNode? Request(string method, JsonNode? parameters)
    {
        int identifier = ++requestIdentifier;
        Write(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = identifier, ["method"] = method, ["params"] = parameters });
        while (true)
        {
            JsonNode message = ReadAsync().GetAwaiter().GetResult();
            if (message["id"]?.ToString() != identifier.ToString(CultureInfo.InvariantCulture))
            {
                continue;
            }

            if (message["error"] is { } error)
            {
                throw new InvalidOperationException(error.ToJsonString());
            }

            return message["result"];
        }
    }
    private void Notify(string method, JsonNode? parameters) => Write(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters });
    private void Write(JsonNode message)
    {
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString());
        input.Write(Encoding.ASCII.GetBytes("Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n"));
        input.Write(body); input.Flush();
    }
    private async Task<JsonNode> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var header = new StringBuilder();
        byte[] single = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (header.Length > 16384)
            {
                throw new InvalidDataException("Oversized language server header.");
            }

            await output.ReadExactlyAsync(single, timeout.Token).ConfigureAwait(false);
            header.Append((char)single[0]);
        }
        int length = 0;
        foreach (string line in header.ToString().Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                length = int.Parse(line[15..].Trim(), CultureInfo.InvariantCulture);
            }
        }

        if (length <= 0 || length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Invalid language server frame length.");
        }

        byte[] body = new byte[length];
        await output.ReadExactlyAsync(body, timeout.Token).ConfigureAwait(false);
        return JsonNode.Parse(body) ?? throw new InvalidDataException("Empty language server response.");
    }
}
