#Requires -Version 7.0
<#
.SYNOPSIS
    Converts BenchmarkDotNet reports into github-action-benchmark's "customSmallerIsBetter" format: one file of
    time series (ns, median) and one of allocation series (bytes per operation).

.DESCRIPTION
    The action's built-in BenchmarkDotNet parser records time only, and keys history to this repository's commit.
    Converting first lets CI track allocations too (deterministic, so they take a tight regression threshold) and
    stamps every data point with the viu build it measured (CI package version and commit). Failed benchmarks reject the history point.

.EXAMPLE
    # Use the paired checkout version and SHA recorded in run.json.
    ./scripts/ConvertTo-BenchmarkHistory.ps1 -Path results/libraries/Runtime -TimeOutput time.json -AllocationOutput alloc.json -ViuVersion 10.0.0-preview.1 -ViuCommit (git -C ../viu rev-parse HEAD)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$TimeOutput,
    [Parameter(Mandatory)][string]$AllocationOutput,
    [string]$ViuVersion = 'unknown',
    [string]$ViuCommit = 'unknown',
    [string]$Runner = $env:RUNNER_NAME
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BenchmarkReports.psm1') -Force

$rows = @(Get-BenchmarkResult -Path $Path)
if ($rows.Count -eq 0) { throw "No BenchmarkDotNet *-report-full.json under $Path." }

if (@($rows | Where-Object { $_.Manifest -and $_.Manifest.profile -eq 'dry' }).Count -gt 0) { throw 'Dry validation runs are not measurement history.' }
$failed = @($rows | Where-Object Failed)
foreach ($row in $failed) { Write-Warning "No statistics for $($row.Name) ($($row.Report)); left out of the history." }

if ($failed.Count -gt 0) { throw 'Failed benchmark cases cannot become a partial history point.' }

$context = "viu $ViuVersion ($($ViuCommit.Substring(0, [Math]::Min(12, $ViuCommit.Length))))" + $(if ($Runner) { " on $Runner" } else { '' })

$time = foreach ($row in $rows | Where-Object { -not $_.Failed }) {
    [ordered]@{
        name  = $row.Name
        unit  = 'ns'
        value = [Math]::Round($row.MedianNs, 3)
        range = '± ' + [Math]::Round($row.StdDevNs, 3)
        extra = "$context`nmean $([Math]::Round($row.MeanNs, 3)) ns"
    }
}

$allocations = foreach ($row in $rows | Where-Object { -not $_.Failed -and $null -ne $_.Bytes }) {
    [ordered]@{
        name  = "$($row.Name) - allocated"
        unit  = 'B'
        value = $row.Bytes
        extra = $context
    }
}

ConvertTo-Json -InputObject @($time) -Depth 4 | Set-Content -Path $TimeOutput -Encoding utf8
ConvertTo-Json -InputObject @($allocations) -Depth 4 | Set-Content -Path $AllocationOutput -Encoding utf8
Write-Host "Wrote $(@($time).Count) time and $(@($allocations).Count) allocation series ($($failed.Count) failed) for $context."
