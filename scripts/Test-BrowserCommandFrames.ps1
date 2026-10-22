#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ResultsPath,[string]$BaselinePath=(Join-Path $PSScriptRoot '../benchmarks/baselines/BrowserCommandFrames.json'))
$ErrorActionPreference='Stop'
$actual=Get-Content -LiteralPath $ResultsPath -Raw|ConvertFrom-Json
$baseline=Get-Content -LiteralPath $BaselinePath -Raw|ConvertFrom-Json
if($actual.engine -ne $baseline.engine -or $actual.runtime -ne $baseline.runtime){throw 'Browser command-frame baseline host does not match.'}
$seen=@{}
foreach($measurement in $actual.measurements){
    if($seen.ContainsKey($measurement.scenario)){throw 'Duplicate browser scenario.'}
    $seen[$measurement.scenario]=$true
    $ceiling=$baseline.ceilings.PSObject.Properties[$measurement.scenario]
    if($null -eq $ceiling){throw "Unreviewed browser scenario: $($measurement.scenario)"}
    $counts=@($measurement.commandFrames)
    if($counts.Count -ne $actual.samples -or $counts.Count -eq 0){throw "Incomplete samples: $($measurement.scenario)"}
    foreach($count in $counts){if($count -lt 1 -or $count -gt $ceiling.Value){throw "Command-frame regression: $($measurement.scenario) used $count; ceiling $($ceiling.Value)."}}
}
foreach($ceiling in $baseline.ceilings.PSObject.Properties){if(-not $seen.ContainsKey($ceiling.Name)){throw "Missing browser scenario: $($ceiling.Name)"}}
Write-Output "PASS: all $($seen.Count) scenarios stay within explicit command-frame ceilings."