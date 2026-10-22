# Viu benchmarks

Packaged-consumer performance coverage for Viu, organized by area using the Cohesion benchmark
layout, shared profiles, comparison tooling, and separate time/allocation history series.
Viu source stays in the sibling repository. Every measured framework reference comes from packages.

```powershell
pwsh ../viu/scripts/Install-Local.ps1
dotnet build Assimalign.Viu.Benchmarks.slnx -c Release
pwsh scripts/Invoke-Benchmarks.ps1 -List
pwsh scripts/Invoke-Benchmarks.ps1 -ExcludeArea Editor -Profile dry
pwsh scripts/Invoke-Benchmarks.ps1 -Area Runtime -Profile ci -Output .artifacts/runtime-run
pwsh scripts/Invoke-BrowserBenchmarks.ps1 -Output .artifacts/browser-run
```

See [CONVENTIONS.md](CONVENTIONS.md) for profiles, provenance, NativeAOT, editor preparation,
coverage boundaries, comparisons, and history. [The measurement viewer](site/index.html) opens
downloaded time/allocation history JSON files locally; no external scripts or account are required.

## Coverage

| Area | Subjects |
| --- | --- |
| Runtime | Reactive subscribers, computed caching, batching, component descriptions, render frames, store lookup/lifetime, all deterministic renderer scenarios |
| Browser | Component registration microbenchmarks; separate trimmed WASM app with eight row operations, live command-frame counts, and managed assembly payload sizes |
| Router | Static/dynamic/missing matching across route-table sizes |
| ServerRenderer | Virtual-tree output and compiled fixture render paths |
| Syntax | CSS parsing/writing/scoping; canonical and compatible containers; template parsing |
| Utilities | Candidate parsing, full utility compilation, and folder-route table creation |
| DevTools | Empty, single-message, and 100-message protocol encoding/decoding |
| Compiler | Projection and source emission across template sizes, including scoped styles |
| Editor | Actual packaged language-server completion, semantic tokens, folding, symbols, and edit/complete requests across document sizes |

## Gates and reviewed budgets

The existing deterministic DOM-operation proxy gate remains unchanged:

```powershell
dotnet test harness/Assimalign.Viu.Testing.Benchmarks/test -c Release
dotnet run --project harness/Assimalign.Viu.Testing.Benchmarks/src -c Release -- interop --gate
dotnet test scripts/tests/Assimalign.Viu.PublishBudget.Tests -c Release
```

`benchmarks/baselines/InteropCounts.json` and `scripts/budgets/PublishBudgets.json` retain their
existing ceilings. `BrowserCommandFrames.json` defines the reviewable actual-frame ceilings.
Scripts never rewrite a baseline from results. The WASM runner
fails missing scenarios, incomplete measurements, and command-frame regressions.

The existing application size/startup gates continue to consume Viu's packaged test fixture:

```powershell
pwsh scripts/Measure-PublishBudget.ps1 -RepositoryRoot ../viu -SampleName EndToEndBrowserApp
pwsh ../viu/scripts/Test-EndToEnd.ps1 -MeasureStartup
pwsh scripts/Test-StartupBudget.ps1 -ResultsPath ../viu/_out/test-results/end-to-end/startup.json
```

For a different already-published app, `Measure-PublishBudget.ps1 -MeasureOnlyPayload
-PublishDirectory <directory> -ResultsPath <json>` reuses the same raw/brotli sizing implementation
without applying an unrelated application's budget. DLLs and WebCIL wrappers receive individual
assembly rows; native runtime WASM is excluded from the managed-assembly subtotal.

CI validates deterministic gates and area execution, records full measurements on scheduled/manual
runs, and uploads separate Linux JIT, Windows editor, and Chromium WASM artifacts with provenance.
Manual runs accept a Viu ref for coordinated branch validation. CI has not run merely because these
workflow files exist. Generated reports and history belong in ignored `.artifacts/` or CI artifacts;
do not commit machine-specific measurements or result summaries.
