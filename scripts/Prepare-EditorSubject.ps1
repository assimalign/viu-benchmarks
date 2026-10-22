[CmdletBinding()]
param([Parameter(Mandatory)][string]$Vsix)
$ErrorActionPreference='Stop'
$repositoryDirectory=Split-Path $PSScriptRoot -Parent
$runtimeIdentifier=[System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
if ($runtimeIdentifier -notin @('win-arm64','win-x64')) { throw 'The Visual Studio subject requires a Windows benchmark leg.' }
$destination=Join-Path $repositoryDirectory '.artifacts/subjects/editor'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Vsix).Path)
try {
    $entry=$archive.GetEntry("LanguageServer/$runtimeIdentifier/Assimalign.Viu.LanguageServer.exe")
    if (-not $entry) { throw "The VSIX has no language server for $runtimeIdentifier." }
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $destination 'Assimalign.Viu.LanguageServer.exe'),$true)
} finally { $archive.Dispose() }
@{ vsix=(Resolve-Path -LiteralPath $Vsix).Path; sha256=(Get-FileHash -LiteralPath $Vsix -Algorithm SHA256).Hash; runtimeIdentifier=$runtimeIdentifier } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'subject.json') -Encoding utf8
Write-Output "Prepared packaged editor subject for $runtimeIdentifier."
