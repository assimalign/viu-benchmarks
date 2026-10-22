using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Runtime.Benchmarks;

// Construction is what every compiled render pays; empty collections are deliberately explicit.
public class DescriptionBenchmarks
{
    private readonly ElementBinding[] emptyBindings = [];
    private readonly VirtualNode[] emptyChildren = [];
    private readonly Dictionary<string, ComponentEventListener> emptyListeners = [];

    [Benchmark, BenchmarkCategory("Aot")]
    public ComponentInvocation EmptyInvocation() => new(listeners: emptyListeners);

    [Benchmark]
    public ElementNode EmptyElement() => new(new QualifiedName("div"), emptyBindings, emptyChildren);

    [Benchmark]
    public ElementNode BoundElement() => new(new QualifiedName("button"),
        [ElementBinding.Attribute(new QualifiedName("class"), "primary")], [new TextNode("Save")]);

    [Benchmark]
    public ComponentRenderFrame RenderFrame() => new(8);
}
