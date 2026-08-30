# viu-benchmarks

Performance measurement for [Viu](https://github.com/assimalign/viu): the benchmark suite, the
deterministic interop-count regression gate, and the reviewed browser publish/startup budgets.

**All Viu benchmarking belongs here.** The Viu repository holds no benchmark projects; a benchmark
added there is a mistake, not a shortcut. See `.claude/rules/benchmarks.md` in `assimalign/viu`.

## What is in here

| Path | What it is |
| --- | --- |
| `benchmarks/Assimalign.Viu.Testing.Benchmarks` | Console app with three verbs: `benchmarks` (BenchmarkDotNet wall-clock), `interop` (the deterministic node-op counter), `browser` (deferred, [#88]). |
| `benchmarks/Assimalign.Viu.Testing.Benchmarks.Tests` | Tests for the harness plumbing: scenario discovery, counters, baseline load/compare, and that a keyless bypass trips the gate. |
| `benchmarks/baselines/InteropCounts.json` | The reviewed interop-crossing ceilings. Raised only by an explicit edit — never ratcheted by CI. |
| `scripts/Measure-PublishBudget.ps1` | Publish-size and trim-warning gate over the brotli-compressed payload. |
| `scripts/Test-StartupBudget.ps1` | Boot-to-interactive startup gate over the median of the measured Chromium runs. |
| `scripts/budgets/PublishBudgets.json` | The reviewed size and startup budgets, with measured provenance. |
| `scripts/tests/Assimalign.Viu.PublishBudget.Tests` | Hermetic tests driving both checkers over on-disk fixtures. |

## The two measurement kinds, and why they are wired differently

**Wall-clock timings never gate.** They are environment-relative, so the `timings` lane is opt-in
(`workflow_dispatch`, or the `benchmark-run` label) and publishes artifacts.

**Interop counts do gate.** Counting node operations through the in-memory adapter is a DOM-free
proxy for JSImport crossings — exact and machine-independent — so a regression against the reviewed
baseline is a real failure.

**Budgets gate on reviewed ceilings, not on deltas.** CI never rewrites
`scripts/budgets/PublishBudgets.json`; growth is always a deliberate, reviewed edit.

## Repository layout: this repo measures, Viu is measured

This repository contains no framework source. It consumes Viu as **packages**, and the measurement
subject for the budget gates — the packaged-consumer fixture and the Playwright harness that
publishes and drives it — stays in the Viu repository:

- `assimalign/viu` owns `scripts/Test-EndToEnd.ps1`, `scripts/fixtures/EndToEndBrowserApp`, and
  `benchmarks/Assimalign.Viu.Testing.EndToEnd`.
- This repository owns the budgets and the checkers that enforce them.

Both budget checkers were built for that split: `-ManifestPath` defaults to the manifest beside the
script (ours), while a sample's `project` and `publishScript` resolve against `-RepositoryRoot`
(Viu's). That is why the gates run as `-RepositoryRoot ../viu`.

## Local setup

Check `assimalign/viu` out as a **sibling directory**, then populate its package feed:

```bash
pwsh ../viu/scripts/Install-Local.ps1 -SkipSdk -SkipFramework
```

`nuget.config` resolves `viu-local` as `..\viu\_out\packages` relative to the configuration file, so
the sibling layout is required — CI reproduces it by checking both repositories out side by side.
`-SkipSdk -SkipFramework` packs only the libraries the benchmarks need and avoids the WebAssembly
workload; drop both switches when you also want to run the budget lanes locally.

```bash
dotnet build Assimalign.Viu.Benchmarks.slnx
dotnet test benchmarks/Assimalign.Viu.Testing.Benchmarks.Tests/Assimalign.Viu.Testing.Benchmarks.Tests.csproj
```

Run the gate and the benchmarks:

```bash
dotnet run --project benchmarks/Assimalign.Viu.Testing.Benchmarks --configuration Release -- interop --gate
```

```bash
dotnet run --project benchmarks/Assimalign.Viu.Testing.Benchmarks --configuration Release -- benchmarks --smoke --filter '*'
```

Both checker scripts require PowerShell 7 (`#Requires -Version 7.0`); Windows PowerShell 5.1 cannot
parse them.

## Which Viu version is measured

`build/Targets/Build.References.Packages.targets` pins `ViuVersion` for local work, but the pin is
**overridable and CI overrides it**. Every CI lane packs the paired Viu checkout at HEAD and passes
`-p:ViuVersion=<packed version>`, so the interop gate keeps measuring the current renderer rather
than a frozen pin. To reproduce a CI run locally, pack Viu and pass the same switch:

```bash
dotnet build Assimalign.Viu.Benchmarks.slnx -p:ViuVersion=10.0.0-beta.11
```

Because Viu's renderer changes land in another repository, the `benchmarks` workflow also runs
daily and `budget-gates` weekly — a path filter here cannot observe a change made there.

[#88]: https://github.com/assimalign/viu/issues/88
