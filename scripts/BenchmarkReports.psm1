#Requires -Version 7.0
# Reads BenchmarkDotNet full JSON reports (*-report-full.json) into flat rows. Shared by
# ConvertTo-BenchmarkHistory.ps1 and Compare-Benchmarks.ps1.

function Get-BenchmarkResult {
    <#
    .SYNOPSIS
        One row per benchmark case in every *-report-full.json under -Path: a stable name (Type.Method(Params)),
        median and standard deviation in nanoseconds, mean, and allocated bytes per operation. Failed cases (no
        statistics) come back with Failed = $true.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $files = Get-ChildItem -Path $Path -Recurse -File -Filter '*-report-full.json'
    foreach ($file in $files) {
        $manifest = $null
        $directory = $file.Directory
        $root = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
        while ($null -ne $directory -and ($directory.FullName -eq $root -or $directory.FullName.StartsWith($root+[IO.Path]::DirectorySeparatorChar))) {
            $candidate = Join-Path $directory.FullName 'run.json'
            if (Test-Path -LiteralPath $candidate) { $manifest=Get-Content $candidate -Raw|ConvertFrom-Json; break }
            $directory=$directory.Parent
        }
        $report = Get-Content $file.FullName -Raw | ConvertFrom-Json -Depth 64
        foreach ($benchmark in $report.Benchmarks) {
            $name = "$($benchmark.Namespace).$($benchmark.Type).$($benchmark.MethodTitle)"
            if ($benchmark.Parameters) { $name += "($($benchmark.Parameters))" }

            $statistics = $benchmark.Statistics
            [pscustomobject]@{
                Name      = $name
                Namespace = $benchmark.Namespace
                Failed    = $null -eq $statistics
                MedianNs  = if ($statistics) { [double]$statistics.Median } else { $null }
                MeanNs    = if ($statistics) { [double]$statistics.Mean } else { $null }
                StdDevNs  = if ($statistics) { [double]$statistics.StandardDeviation } else { $null }
                Bytes     = if ($null -ne $benchmark.Memory -and $null -ne $benchmark.Memory.BytesAllocatedPerOperation) { [double]$benchmark.Memory.BytesAllocatedPerOperation } else { $null }
                Report    = $file.Name
                Manifest  = $manifest
            }
        }
    }
}

function Format-Duration {
    param([double]$Nanoseconds)
    if ($Nanoseconds -ge 1e9) { return '{0:N2} s' -f ($Nanoseconds / 1e9) }
    if ($Nanoseconds -ge 1e6) { return '{0:N2} ms' -f ($Nanoseconds / 1e6) }
    if ($Nanoseconds -ge 1e3) { return '{0:N2} us' -f ($Nanoseconds / 1e3) }
    return '{0:N1} ns' -f $Nanoseconds
}

Export-ModuleMember -Function Get-BenchmarkResult, Format-Duration
