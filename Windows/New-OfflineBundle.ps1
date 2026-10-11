#Requires -Version 5.1
<#
.SYNOPSIS
Builds a self-contained Windows offline installer ZIP on a PC with .NET 10 SDK.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '../artifacts/offline'
}
$project = Join-Path $PSScriptRoot 'RunnerDuck.Desktop/RunnerDuck.App.csproj'
$rid = 'win-' + $Platform.ToLowerInvariant()
$buildProperties = @('-p:Configuration=Release', "-p:Platform=$Platform", "-p:RuntimeIdentifier=$rid",
    '-p:SelfContained=true', '-p:WindowsAppSDKSelfContained=true')
& dotnet build $project -t:Rebuild @buildProperties
if ($LASTEXITCODE -ne 0) { throw 'Offline bundle build failed.' }
$output = & dotnet msbuild $project -nologo @buildProperties -getProperty:TargetDir
if ($LASTEXITCODE -ne 0) { throw 'Could not locate build output.' }
$layout = ($output -join [Environment]::NewLine).Trim()
foreach ($file in @('AppxManifest.xml', 'RunnerDuck.exe', 'coreclr.dll', 'hostfxr.dll', 'Microsoft.WindowsAppRuntime.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $layout $file))) { throw "Missing offline component: $file" }
}
$bundleName = "RunnerDuck-windows-$Platform-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$bundle = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $bundleName
$windows = Join-Path $bundle 'Windows'
$null = New-Item -ItemType Directory -Path $windows -Force
Copy-Item -LiteralPath $layout -Destination (Join-Path $windows 'Payload') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../install.ps1') -Destination $bundle
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../install.cmd') -Destination $bundle
foreach ($file in @('Install-RunnerDuck.ps1', 'InstallerComponents.ps1', 'InstallerPrerequisites.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $windows
}
@'
RunnerDuck offline installation
1. Extract the entire ZIP on a Windows PC matching the bundle architecture.
2. Run: pwsh -File ./install.ps1
   Or double-click install.cmd (also works without PowerShell 7).
3. Choose 1: Widget + Tray, 2: Widget only, or 3: Tray only.
Tray needs no Developer Mode or administrator permission.
Widget modes enable Developer Mode automatically; approve the Windows UAC prompt if shown.
No .NET SDK, PowerShell download, or package restore is needed on the target PC.
For unattended installation: add -InstallMode Both (or Widget / Tray) and -NoLaunch.
Widgets require Windows 11 and an existing Windows Widgets board. Add with Win+W.
AI usage refresh still requires internet access; offline installation does not make online services available offline.
'@ | Set-Content -LiteralPath (Join-Path $bundle 'INSTALL.txt') -Encoding UTF8
$zip = "$bundle.zip"
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Offline installer created: $zip"
