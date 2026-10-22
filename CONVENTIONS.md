# Benchmark conventions

## Structure and subjects

Use `benchmarks/{libraries,tooling}/<Area>/benchmarks/Assimalign.Viu.<Area>.Benchmarks/src`.
Keep one subject folder per measured package and one suite per area. Shared process/profile/reporting
code belongs in `harness/Assimalign.Viu.Benchmarks.Harness/src`; executable browser subjects live under
their area's `subjects/`, outside suite discovery. All projects belong in the solution.
The deterministic renderer gate and reusable legacy benchmark fixtures live in
`harness/Assimalign.Viu.Testing.Benchmarks/{src,test}`; retain the existing assembly identities.

Reference framework packages with `ViuBenchmarkPackageReference`; versions belong in the central
package catalog. Project references are only for this repository's harnesses/fixtures. Compiler
subjects use the shipped App.Ref analyzer closure; editor subjects are extracted from the actual
VSIX. Never reference sibling source or loose sibling build DLLs.

Local Viu packs reuse a version. Select a fresh `NUGET_PACKAGES` directory under `.nuget/` after
repacking; an active MSBuild process may hold old SDK files open. Build before measuring. `-NoBuild`
is only for the binaries just built with the same package version. Do not mix a fresh feed with stale
consumer binaries and call that a measurement of HEAD.

## Profiles and host separation

- `dry`: one execution for correctness and discovery. These numbers are not history points.
- `ci`: bounded statistical pass, one launch, five warmups, twenty measured iterations.
- `local`: longer investigation profile.
- JIT is in process; `--out-of-process` selects process isolation. NativeAOT is a separate runtime
  (`-Runtime aot`) and runs only cases explicitly carrying `BenchmarkCategory("Aot")`. It needs
  the platform's native compiler toolchain. A dry JIT run does not validate NativeAOT.

Keep OS, architecture, runtime, profile, and browser engine in separate result legs. CoreCLR timings
do not represent browser timings. Editor allocation results measure the client transport; server
memory and Visual Studio painting are separate questions. The editor workload currently has no
project workspace and therefore measures the server's syntax/fallback path, not project compilation.
The HTML parser is currently a located document scaffold; there is no fabricated full-HTML parsing
benchmark. Framework/SDK pack producers are containers, measured through packaging/consumer checks.

## Running and provenance

`Invoke-Benchmarks.ps1` discovers area suites, builds Release, and chooses a fresh artifact directory.
Each suite gets `run.json` with Viu version/SHA, dirty state, SDK, OS, architecture, profile/runtime,
UTC time, and local package SHA-256 hashes. Editor manifests include the VSIX subject hash. Preserve
these manifests alongside full BenchmarkDotNet reports in ignored `.artifacts/` or CI artifacts.
Do not commit generated run data, machine-specific result summaries, or history snapshots.
Reviewed baseline and budget manifests remain source-controlled inputs. An uncommitted build is explicitly dirty;
its recorded SHA is not a claim that the changes were committed.

```powershell
pwsh scripts/Prepare-EditorSubject.ps1 -Vsix ../viu/_out/extensions/VisualStudio/Debug/Assimalign.Viu.VisualStudio.vsix
pwsh scripts/Invoke-Benchmarks.ps1 -Area Editor -Scope tooling -Profile ci
pwsh scripts/Compare-Benchmarks.ps1 -Baseline .artifacts/before -Current .artifacts/after -FailOnRegression
pwsh scripts/ConvertTo-BenchmarkHistory.ps1 -Path .artifacts/after -TimeOutput .artifacts/history/time.json -AllocationOutput .artifacts/history/allocations.json -ViuVersion 10.0.0-preview.1 -ViuCommit <sha>
```

Comparisons use namespace/type/method/parameters, reject duplicate cases or incompatible run legs,
and fail missing/failed cases. Default limits are 1.10 times median time and 1.00 times allocations.
Treat timing flags as investigation signals and repeat controls on an idle, comparable host.
History conversion refuses failed partial runs. Open `site/index.html` and select history files from
the same host leg to inspect values; CI retains reports/manifests as artifacts for 90 days.

## Browser measurements

The runner publishes a trimmed Release WASM subject and serves its real output. A JavaScript timer
starts immediately before a click and ends when the render acknowledgement changes; Playwright
transport time is outside the measured interval. Reset work is outside each sample. There are three
warmups and twenty samples by default, with individual samples retained. Each scenario verifies the
rendered row count; update checks changed labels, selection checks its class, swap checks keyed DOM
identity, and replace/remove check the outgoing first identifier. The subject-only wrapper counts actual `applyCommandBuffer` calls without adding
instrumentation to shipping Viu. This is a command-frame counter, not a count of every possible JS
interop call. DOM-operation proxy counts remain a separate deterministic gate.

Create 1,000/10,000 rows, replace, update every tenth, select, swap, remove, and clear are covered.
Startup and payload budgets remain separate from scenario time. No allocation claim is inferred
from browser timing. Baseline edits are explicit reviewable source changes, never automatic ratchets.
