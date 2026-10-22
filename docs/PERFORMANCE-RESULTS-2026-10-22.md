# Performance investigation — 2026-10-22

These are local measurements of uncommitted work on
`feature/V01.01.12.37-framework-and-performance`, package `10.0.0-preview.1`.
The recorded Viu SHA is the starting commit, not a commit containing these changes. Windows ARM64,
Snapdragon X1E80100, SDK 10.0.402, runtime 10.0.13, BenchmarkDotNet 0.15.8, Chromium 147.0.7727.15.
[Machine-readable evidence](results/2026-10-22-windows-arm64.json) retains individual browser samples,
package hashes, and allocation-report hashes.

## Accepted changes

| Change | Before | After | Interpretation |
| --- | ---: | ---: | --- |
| Empty `ComponentInvocation` allocation | 184 B | 64 B | 120 B / 65.2% less per description; shared immutable empty dictionaries avoid redundant wrappers. Nonempty dictionaries are pre-sized. |
| Update every tenth row, actual applied command frames | 101 | 2 | 98.0% fewer frames for 1,000 keyed rows / 100 changed labels; stable native block children no longer force per-child parent/sibling reads. |
| Bound element allocation control | 384 B | 384 B | Populated binding snapshot cost is unchanged. |

Dictionary snapshot tests pin independence from caller mutations, including initially empty mutable
inputs. Renderer tests pin stable element/text/comment patches and nested type/key replacement
fallback. Replacements still discover their actual parent and anchor. The real browser runner checks
row counts, changed labels, selected classes, keyed DOM identity, and outgoing identifiers outside
the measured interval. Existing deterministic operation ceilings are unchanged.

Two broader array/list snapshot optimization attempts were rejected after measurement and reverted.
The accepted change is intentionally limited to dictionary storage. Timing controls showed concurrent
host-load contamination, so the nanosecond changes are not an accepted speedup claim.

## Current browser scenario reference

Three warmups and twenty measured samples per scenario, Release trimmed WASM. Reset work and
Playwright transport are outside the measured interval. All twenty samples use two command frames
in every scenario. Medians below describe this host; they are not portable budgets or causal A/B gains.

| Scenario | Median milliseconds |
| --- | ---: |
| create | 48.80 |
| create10000 | 448.40 |
| replace | 59.35 |
| update | 37.10 |
| select | 36.75 |
| swap | 38.00 |
| remove | 37.70 |
| clear | 14.45 |

The exploratory baseline had five samples and predates complete run manifests. Its retained output
shows 101 update frames; the optimized five-sample run and final twenty-sample run both show two.
Do not infer a precise latency improvement by comparing those exploratory runs.

The dedicated benchmark subject totals 8,087,771 raw bytes and 2,341,520 brotli bytes, with seventeen
managed assemblies reported separately. This is a different app from the reviewed application budget.
The packaged EndToEndBrowserApp remains within its existing 2,700,000-byte budget at 2,505,716 brotli
bytes. Its startup median is 1,252.2 ms over ten fresh contexts after one warmup, below the existing
1,800 ms budget plus 200 ms noise tolerance. Neither reviewed ceiling was raised.

## Coverage and validation

Nine areas execute 73 BenchmarkDotNet cases: Browser 2, DevTools 6, Router 6, Runtime 23,
ServerRenderer 4, Syntax 12, Utilities 6, Compiler 4, Editor 10. These dry runs verify execution and
observable setup results; they are deliberately rejected by history conversion. One explicitly opted-in
NativeAOT description case also compiles and executes on this host. The actual VSIX server handles
completion, semantic tokens, folding, symbols, and edit/complete requests in its own process. A packed
consumer semantic round trip separately verifies real project-backed C# semantics.

The legacy adapter gate passes all nine scenarios; its 23 harness tests and the 31 report/payload/gate
tests pass. Report comparison rejects incompatible hosts, missing or failed cases, and missing current
allocation metrics. Frame-gate tests prove that 101 frames fail the proposed two-frame ceiling.
CI definitions now build paired packages and run independent Linux, Windows editor, and Chromium
legs; they have been parsed locally but have not run on GitHub from this unpushed branch.

## Remaining performance questions

| Priority | Question and next measurement |
| --- | --- |
| 1 | Installed Visual Studio latency: record edit-to-color and completion latency in a real editor with a project. The cached classifier now searches the visible range, but server latency does not measure UI painting. |
| 2 | Creation costs: use #263 to evaluate the fast text-mount contract against both operation count and real frame/time measurements. Most logical operations already batch into two frames. |
| 3 | Routing scale and update allocations: collect statistical histories from the new route-table and renderer cases on idle, fixed runners before changing algorithms. |
| 4 | Projection/CSS/utility compilation: profile large documents and candidate sets with the area suites; keep #353's dedicated utility budgets separate. |
| 5 | Packaging and startup trends: retain per-assembly size and startup results per build; investigate regressions without silently changing ceilings. |

The editor suite currently measures syntax/fallback requests without a project workspace; its
allocation numbers cover the client, not server memory or Visual Studio rendering. The current HTML
parser is a located document scaffold and is not advertised as a full HTML parsing benchmark.
NativeAOT opt-in coverage is one case, not the entire runtime matrix. These limits are explicit so
future measurements can extend coverage without claiming unmeasured performance improvements.
