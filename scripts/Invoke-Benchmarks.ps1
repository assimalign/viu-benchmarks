#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('all','libraries','tooling')][string]$Scope='all',
    [string[]]$Area,
    [string[]]$ExcludeArea,
    [ValidateSet('local','ci','dry')][string]$Profile='local',
    [ValidateSet('jit','aot')][string]$Runtime='jit',
    [string]$Filter='*',
    [string]$ViuVersion,
    [string]$Output,
    [switch]$List,
    [switch]$AsJson,
    [switch]$NoBuild,
    [Parameter(ValueFromRemainingArguments=$true)][string[]]$BenchmarkArguments
)
$ErrorActionPreference='Stop'
$repositoryDirectory=Split-Path $PSScriptRoot -Parent
$ExcludeArea=@($ExcludeArea | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Area=@($Area | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$scopes=if($Scope -eq 'all'){@('libraries','tooling')}else{@($Scope)}
$suites=@(foreach($scopeName in $scopes) {
    $scopeDirectory=Join-Path $repositoryDirectory "benchmarks/$scopeName"
    foreach($areaDirectory in Get-ChildItem -LiteralPath $scopeDirectory -Directory | Sort-Object Name) {
        if(($Area -and $Area -notcontains $areaDirectory.Name) -or $ExcludeArea -contains $areaDirectory.Name){continue}
        $project=Get-ChildItem -LiteralPath (Join-Path $areaDirectory.FullName 'benchmarks') -Recurse -File -Filter '*.Benchmarks.csproj' |
            Where-Object {$_.Directory.Name -eq 'src'}
        foreach($entry in $project) { [pscustomobject]@{scope=$scopeName;area=$areaDirectory.Name;name=$entry.BaseName;project=[IO.Path]::GetRelativePath($repositoryDirectory,$entry.FullName).Replace('\','/')} }
    }
})
if($List){if($AsJson){ConvertTo-Json -InputObject $suites -Compress}else{$suites | Format-Table};return}
if($suites.Count -eq 0){throw 'No benchmark suites match the requested scope and area.'}
if(-not $ViuVersion){
    $catalog=[xml](Get-Content (Join-Path $repositoryDirectory 'build/Targets/Build.References.Packages.targets') -Raw)
    $ViuVersion=$catalog.SelectSingleNode('/Project/PropertyGroup/ViuVersion').InnerText
}
$revision=& git -C (Join-Path $repositoryDirectory '../viu') rev-parse HEAD
$dirty=[bool](& git -C (Join-Path $repositoryDirectory '../viu') status --porcelain)
$runDirectory=if($Output){[IO.Path]::GetFullPath($Output,$repositoryDirectory)}else{Join-Path $repositoryDirectory ('.artifacts/runs/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))}
foreach($suite in $suites){
    $project=Join-Path $repositoryDirectory $suite.project
    if(-not $NoBuild){
        & dotnet build $project -c Release "-p:ViuVersion=$ViuVersion" --nologo -v:quiet
        if($LASTEXITCODE -ne 0){throw "Build failed: $($suite.name)"}
    }
    $destination=Join-Path $runDirectory "$($suite.scope)/$($suite.area)"
    if(Test-Path -LiteralPath $destination){throw "Use a fresh report directory; refusing to mix runs at $destination."}
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $manifest=[ordered]@{suite=$suite.name;scope=$suite.scope;area=$suite.area;profile=$Profile;runtime=$Runtime;viuVersion=$ViuVersion;viuRevision=$revision;viuWorkingTreeDirty=$dirty;os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString();sdk=(& dotnet --version);utc=[DateTime]::UtcNow.ToString('O')}
    $packageDirectory=Join-Path $repositoryDirectory '../viu/_out/packages'
    $manifest.packageHashes=@(Get-ChildItem -LiteralPath $packageDirectory -Filter "*.$ViuVersion.nupkg" | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    if($suite.area -eq 'Editor'){
        $subjectManifest=Join-Path $repositoryDirectory '.artifacts/subjects/editor/subject.json'
        if(-not (Test-Path -LiteralPath $subjectManifest)){throw 'Prepare the editor VSIX subject first.'}
        $manifest.editorSubject=Get-Content $subjectManifest -Raw | ConvertFrom-Json
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'run.json') -Encoding utf8
    Push-Location $repositoryDirectory
    try { & dotnet run --project $project -c Release --no-build -- --profile $Profile --runtime $Runtime --filter "$Filter" --artifacts $destination @BenchmarkArguments }
    finally { Pop-Location }
    if($LASTEXITCODE -ne 0){throw "Benchmark failed: $($suite.name)"}
}
Write-Output "All $($suites.Count) selected suites completed."
