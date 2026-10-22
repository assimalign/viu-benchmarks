#Requires -Version 7.0
[CmdletBinding()]
param([string]$ViuVersion='10.0.0-preview.1',[string]$Output='.artifacts/browser',[int]$Samples=20,[switch]$SkipBrowserInstall)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=[IO.Path]::GetFullPath((Join-Path $root $Output))
if(Test-Path $destination){throw "Choose a fresh output directory: $destination"}
[IO.Directory]::CreateDirectory($destination)|Out-Null
$subject=Join-Path $root 'benchmarks/libraries/Browser/subjects/Assimalign.Viu.Browser.Subject'
$global=Join-Path $subject 'global.json'
$original=[IO.File]::ReadAllText($global)
try {
    $configuration=$original|ConvertFrom-Json
    $configuration.'msbuild-sdks'.'Assimalign.Viu.Sdk'=$ViuVersion
    $configuration.'msbuild-sdks'.'Assimalign.Viu.Sdk.Browser'=$ViuVersion
    $configuration|ConvertTo-Json -Depth 5|Set-Content $global
    Push-Location $subject
    try { & dotnet publish -c Release -warnaserror --output (Join-Path $destination 'published'); if($LASTEXITCODE -ne 0){throw 'WASM publish failed.'} }
    finally {Pop-Location}
} finally {[IO.File]::WriteAllText($global,$original)}
$runner=Join-Path $PSScriptRoot 'BrowserBenchmarks/BrowserBenchmarks.csproj'
& dotnet build $runner -c Release -warnaserror
if($LASTEXITCODE -ne 0){throw 'Browser runner build failed.'}
if(-not $SkipBrowserInstall){& dotnet run --project $runner -c Release --no-build -- --install; if($LASTEXITCODE -ne 0){throw 'Chromium installation failed.'}}
& dotnet run --project $runner -c Release --no-build -- (Join-Path $destination 'published/wwwroot') $destination $Samples
if($LASTEXITCODE -ne 0){throw 'Browser measurements failed.'}
& (Join-Path $PSScriptRoot 'Test-BrowserCommandFrames.ps1') -ResultsPath (Join-Path $destination 'browser-results.json')
if($LASTEXITCODE -ne 0){throw 'Browser command-frame gate failed.'}
$packages=@(Get-ChildItem (Join-Path $root '../viu/_out/packages') -Filter "*.$ViuVersion.nupkg"|ForEach-Object{@{name=$_.Name;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
@{viuVersion=$ViuVersion;viuCommit=(& git -C (Join-Path $root '../viu') rev-parse HEAD);viuDirty=[bool](& git -C (Join-Path $root '../viu') status --porcelain);os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;architecture=[Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString();runtime='browser-wasm';engine='chromium';sdk=(& dotnet --version);utc=[DateTime]::UtcNow.ToString('o');packages=$packages}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $destination 'run.json')
# Reuse the reviewed payload measurement path; informational mode never changes a budget.
& (Join-Path $PSScriptRoot 'Measure-PublishBudget.ps1') -PublishDirectory (Join-Path $destination 'published') -MeasureOnlyPayload -ResultsPath (Join-Path $destination 'payload.json')
if($LASTEXITCODE -ne 0){throw 'Payload measurement failed.'}