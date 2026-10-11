#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommit
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$verificationDirectory = Join-Path ([IO.Path]::GetTempPath()) ('RunnerDuck-update-build-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $verificationDirectory
try {
    & (Join-Path $PSScriptRoot 'New-OfflineBundle.ps1') -Platform x64 -OutputDirectory $verificationDirectory
    $archives = @(Get-ChildItem -LiteralPath $verificationDirectory -Filter '*.zip')
    if ($archives.Count -ne 1) { throw 'Expected one offline ZIP.' }
    $archive = $archives[0]
    if ($null -eq $archive) { throw 'No offline ZIP was produced.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'Windows/Payload/RunnerDuck.dll' } | Select-Object -First 1
        if ($null -eq $entry) { throw 'The offline ZIP has no RunnerDuck app assembly.' }
        $assembly = Join-Path $verificationDirectory 'RunnerDuck.dll'
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $assembly)
    } finally {
        $zip.Dispose()
    }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).ProductVersion
    if (-not $version.EndsWith('+' + $ExpectedCommit, [StringComparison]::Ordinal)) {
        throw "Packaged app revision mismatch: $version"
    }
    Write-Host "Verified offline app build revision: $version"
} finally {
    Remove-Item -LiteralPath $verificationDirectory -Recurse -Force
}
