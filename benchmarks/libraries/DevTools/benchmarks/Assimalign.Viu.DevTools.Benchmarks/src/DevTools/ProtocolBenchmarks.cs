using System.Collections.Generic;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Benchmarks;

public class ProtocolBenchmarks
{
    private readonly List<ProtocolEnvelope> messages = [];
    private string encoded = "";
    [Params(0, 1, 100)] public int Messages { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        messages.Clear();
        using JsonDocument payload = JsonDocument.Parse("{\"supportedVersions\":[1]}");
        for (int index = 0; index < Messages; index++) { messages.Add(new ProtocolEnvelope(1, "handshake.request", payload.RootElement.Clone())); }
        encoded = ProtocolCodec.SerializeBatch(messages);
    }
    [Benchmark] public string SerializeBatch() => ProtocolCodec.SerializeBatch(messages);
    [Benchmark] public ProtocolBatch? DeserializeBatch() => ProtocolCodec.DeserializeBatch(encoded);
}