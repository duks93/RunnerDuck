#Requires -Version 5.1
<#
.SYNOPSIS
Builds, installs, and starts RunnerDuck for the current Windows user.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform,
    [ValidateSet('Both', 'Widget', 'Tray')]
    [string]$InstallMode,
    [switch]$NoLaunch
)

& (Join-Path $PSScriptRoot 'Windows/Install-RunnerDuck.ps1') @PSBoundParameters
exit $LASTEXITCODE
