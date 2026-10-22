using System.Linq;
using BenchmarkDotNet.Attributes;

using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Benchmarks;

public class TemplateBenchmarks
{
    private string template = "";
    private string canonical = "";
    private string compatible = "";

    [Params(1, 100)] public int Elements { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        template = string.Concat(Enumerable.Repeat("<p :class=\"Name\">{{ Name }}</p>\n", Elements));
        canonical = "<template>\n" + template + "</template>\n@script { public string Name => \"Viu\"; }\n<style scoped>p { color: red; }</style>";
        compatible = canonical.Replace("@script { public string Name => \"Viu\"; }", "<script>public string Name => \"Viu\";</script>");
    }
    [Benchmark] public object ParseTemplate() => TemplateParser.Parse(template);
    [Benchmark] public object ParseCanonicalContainer() => SingleFileComponentParser.Parse(canonical);
    [Benchmark] public object ParseCompatibleContainer() => VueSingleFileComponentParser.Parse(compatible);

}