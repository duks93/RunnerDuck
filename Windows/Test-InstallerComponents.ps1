#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerComponents.ps1')
foreach ($mode in @('Both', 'Widget', 'Tray', 'tray')) {
    [xml]$manifest = Get-Content (Join-Path $PSScriptRoot 'RunnerDuck.Desktop/Package.appxmanifest') -Raw
    Set-RunnerDuckInstallMode -Manifest $manifest -InstallMode $mode
    $categories = @($manifest.SelectNodes('//*[local-name()="Extension"]') | ForEach-Object { $_.GetAttribute('Category') })
    if (($categories -contains 'windows.startupTask') -ne ($mode -ne 'Widget')) { throw "$mode startup registration mismatch" }
    if (($categories -contains 'windows.comServer') -ne ($mode -ne 'Tray')) { throw "$mode COM registration mismatch" }
    if (($categories -contains 'windows.appExtension') -ne ($mode -ne 'Tray')) { throw "$mode widget registration mismatch" }
    if ($manifest.Package.Identity.Name -ne 'RunnerDuck') { throw 'Package identity changed' }
}
Write-Host 'Installer component checks passed.'
