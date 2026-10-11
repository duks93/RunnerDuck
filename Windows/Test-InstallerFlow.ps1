#Requires -Version 5.1
param([string]$Case)
$ErrorActionPreference = 'Stop'
if (-not $Case) {
    foreach ($scenario in @('Tray', 'Both', 'Widget', 'PackageToTray', 'TrayToBoth')) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Case $scenario
        if ($LASTEXITCODE -ne 0) { throw "Installer scenario failed: $scenario" }
    }
    Write-Host 'Offline installer flows passed (including Developer Mode off and mode migration).'
    exit 0
}

# Run the real scripts against small fake payloads. Only Windows integration is
# mocked; no user's package, registry, process, or Start menu entry is modified.
$global:fixture = Join-Path $PSScriptRoot "../artifacts/installer-tests/$Case-$([guid]::NewGuid())"
$windows = Join-Path $global:fixture 'Windows'
$payload = Join-Path $windows 'Payload'
$global:testData = Join-Path $global:fixture 'Data'
$global:testPrograms = Join-Path $global:fixture 'Programs'
foreach ($directory in @($payload, $global:testData, $global:testPrograms)) {
    $null = New-Item -ItemType Directory -Path $directory -Force
}
foreach ($name in @('InstallerComponents.ps1', 'InstallerPrerequisites.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $windows
}
$installer = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Install-RunnerDuck.ps1'))
$installer = $installer.Replace("[Environment]::GetFolderPath('LocalApplicationData')", '$global:testData')
$installer = $installer.Replace("[Environment]::GetFolderPath('Programs')", '$global:testPrograms')
$installer = $installer.Replace('[Environment]::OSVersion.Version.Build', '26100')
[IO.File]::WriteAllText((Join-Path $windows 'Install-RunnerDuck.ps1'), $installer, [Text.UTF8Encoding]::new($true))
$mode = switch ($Case) { 'PackageToTray' { 'Tray' }; 'TrayToBoth' { 'Both' }; default { $Case } }
$global:testMode = $mode
$global:developerEnabled = $false
$global:elevations = 0
$global:registrations = 0
$global:removals = 0
$global:package = $null
$global:reads = 0
function New-TestPackage {
    param([string]$Location)
    return [pscustomobject]@{ IsDevelopmentMode = $true; Publisher = 'CN=duks93'; InstallLocation = $Location;
        Status = 'Ok'; Version = '1.0.0.0'; PackageFamilyName = 'RunnerDuck_test'; PackageFullName = 'RunnerDuck_test_1' }
}
if ($Case -eq 'PackageToTray') {
    $global:package = New-TestPackage (Join-Path $global:testData 'RunnerDuck/App')
    $oldData = Join-Path $global:testData 'Packages/RunnerDuck_test/LocalState'
    $null = New-Item -ItemType Directory -Path $oldData -Force
    Set-Content (Join-Path $oldData 'settings.json') '{"migration":true}'
}
if ($Case -eq 'TrayToBoth') {
    $null = New-Item -ItemType Directory -Path (Join-Path $global:testData 'RunnerDuck/TrayApp') -Force
    Set-Content (Join-Path $global:testData 'RunnerDuck/TrayApp/RunnerDuck.exe') 'fixture'
    Set-Content (Join-Path $global:testData 'RunnerDuck/settings.json') '{"migration":true}'
}
[xml]$manifest = Get-Content (Join-Path $PSScriptRoot 'RunnerDuck.Desktop/Package.appxmanifest') -Raw
$manifest.Package.Identity.SetAttribute('ProcessorArchitecture', 'x64')
$manifest.Save((Join-Path $payload 'AppxManifest.xml'))
foreach ($relative in @('RunnerDuck.exe', 'RunnerDuck.dll', 'RunnerDuck.deps.json', 'RunnerDuck.runtimeconfig.json',
    'coreclr.dll', 'hostfxr.dll', 'Microsoft.WindowsAppRuntime.dll', 'resources.pri', 'ko/RunnerDuck.resources.dll',
    'Assets/StoreLogo.png', 'Assets/Square150x150Logo.png', 'Assets/Square44x44Logo.png',
    'ProviderAssets/AIUsage_Icon.png', 'ProviderAssets/AIUsage_Screenshot.png',
    'Duck/animation.json', 'Duck/duck-atlas.png', 'Duck/duck-filled-atlas.png',
    'Legal/NOTICE.txt', 'Legal/LICENSE.txt', 'Legal/THIRD_PARTY.txt')) {
    $path = Join-Path $payload $relative
    $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
    Set-Content -LiteralPath $path 'fixture'
}
function Import-Module { param($Name) }
function Get-Process { param($Name, $ErrorAction) }
function Invoke-WebRequest { throw 'Offline install attempted a download.' }
function dotnet { throw 'Offline install attempted to use an SDK.' }
function Get-ItemPropertyValue {
    param($Path, $Name, $ErrorAction)
    if ($global:testMode -eq 'Tray') { throw 'Tray touched Developer Mode.' }
    return [int]$global:developerEnabled
}
function Start-Process {
    param($FilePath, $ArgumentList, $Verb, $WindowStyle, [switch]$PassThru, [switch]$Wait)
    if ($Verb -ne 'RunAs' -or $global:testMode -eq 'Tray') { throw 'Unexpected elevation or launch.' }
    $global:elevations++
    $global:developerEnabled = $true
    return [pscustomobject]@{ ExitCode = 0 }
}
function Get-AppxPackage { param($Name); return $global:package }
function Add-AppxPackage {
    param($Register)
    if ($global:testMode -eq 'Tray') { throw 'Tray registered a package.' }
    $global:registrations++
    $global:package = New-TestPackage (Split-Path $Register)
}
function Remove-AppxPackage { param($Package); $global:removals++; $global:package = $null }
function Remove-ItemProperty { param($Path, $Name, $ErrorAction) }
function New-Object {
    param($ComObject)
    if ($ComObject -ne 'WScript.Shell') { throw 'Unexpected COM object.' }
    $shell = [pscustomobject]@{}
    $shell | Add-Member ScriptMethod CreateShortcut {
        param($path)
        $shortcut = [pscustomobject]@{ Path = $path; TargetPath = ''; WorkingDirectory = '' }
        $shortcut | Add-Member ScriptMethod Save { Set-Content -LiteralPath $this.Path $this.TargetPath }
        return $shortcut
    }
    return $shell
}
function Read-Host {
    param($Prompt)
    $global:reads++
    if ($global:reads -eq 1) { return 'invalid' }
    return '3'
}
$parameters = @{ Platform = 'x64'; NoLaunch = $true }
if ($Case -ne 'Tray') { $parameters.InstallMode = $mode }
& (Join-Path $windows 'Install-RunnerDuck.ps1') @parameters
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$destination = if ($mode -eq 'Tray') { Join-Path $global:testData 'RunnerDuck/TrayApp' } else { $global:package.InstallLocation }
if ((Get-Content (Join-Path $destination 'install-mode.txt') -Raw).Trim() -ne $mode) { throw 'Wrong saved mode.' }
if ($global:elevations -ne [int]($mode -ne 'Tray')) { throw 'Wrong elevation count.' }
if ($global:registrations -ne [int]($mode -ne 'Tray')) { throw 'Wrong registration count.' }
if ($global:removals -ne [int]($Case -eq 'PackageToTray')) { throw 'Wrong removal count.' }
if ($Case -eq 'Tray' -and $global:reads -ne 2) { throw 'Invalid menu input was not retried.' }
if ($Case -eq 'PackageToTray' -and -not (Test-Path (Join-Path $global:testData 'RunnerDuck/settings.json'))) { throw 'Settings lost switching to Tray.' }
if ($Case -eq 'TrayToBoth' -and -not (Test-Path (Join-Path $global:testData 'Packages/RunnerDuck_test/LocalState/settings.json'))) { throw 'Settings lost switching to widgets.' }
Write-Host "PASS: $Case"
