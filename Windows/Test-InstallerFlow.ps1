#Requires -Version 5.1
param([string]$Case)
$ErrorActionPreference = 'Stop'
if (-not $Case) {
    foreach ($scenario in @('Tray', 'Both', 'Widget', 'PackageToTray', 'TrayToBoth', 'TrayUpdate', 'TrayCleanupFailure',
        'RegistryWriteFailure', 'PackageRegistryWriteFailure', 'InvalidPayload', 'PackageVerificationFailure')) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Case $scenario
        if ($LASTEXITCODE -ne 0) { throw "Installer scenario failed: $scenario" }
    }
    Write-Host 'Offline installer flows passed (including Developer Mode off and mode migration).'
    exit 0
}

# Run the real scripts against small fake payloads. Only Windows integration is
# mocked; registry writes use an isolated test key, never the user's app settings.
$testRegistryPath = 'Software\RunnerDuck.Tests\InstallerFlow-' + [guid]::NewGuid().ToString('N')
$global:testRegistryRoot = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($testRegistryPath)
try {
$global:fixture = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../artifacts/installer-tests/$Case-$([guid]::NewGuid())"))
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
$productionRegistry = '[Microsoft.Win32.Registry]::CurrentUser'
if ([regex]::Matches($installer, [regex]::Escape($productionRegistry)).Count -ne 1) {
    throw 'Installer registry access changed; refusing to run without verified fixture isolation.'
}
$installer = $installer.Replace($productionRegistry, '$global:testRegistryRoot')
if ($installer.Contains($productionRegistry) -or -not $installer.Contains('$global:testRegistryRoot')) {
    throw 'Installer fixture registry isolation failed.'
}
[IO.File]::WriteAllText((Join-Path $windows 'Install-RunnerDuck.ps1'), $installer, [Text.UTF8Encoding]::new($true))
$mode = switch ($Case) {
    'PackageToTray' { 'Tray' }; 'TrayToBoth' { 'Both' }; 'TrayUpdate' { 'Tray' }; 'TrayCleanupFailure' { 'Tray' }
    'RegistryWriteFailure' { 'Tray' }; 'PackageRegistryWriteFailure' { 'Both' }; 'InvalidPayload' { 'Tray' }
    'PackageVerificationFailure' { 'Both' }; default { $Case }
}
$global:testMode = $mode
$global:developerEnabled = $false
$global:elevations = 0
$global:registrations = 0
$global:removals = 0
$global:package = $null
$global:reads = 0
$global:stopped = $false
$global:running = $null
$global:launched = $null
$global:cleanupWarnings = @()
$expectedFailure = $Case -in @('RegistryWriteFailure', 'PackageRegistryWriteFailure', 'InvalidPayload', 'PackageVerificationFailure')
$previousInstalled = Join-Path $global:testData 'RunnerDuck/Previous/RunnerDuck.exe'
if ($expectedFailure -or $Case -in @('TrayToBoth', 'PackageToTray')) {
    $record = $global:testRegistryRoot.CreateSubKey('Software\RunnerDuck')
    try { $record.SetValue('InstalledExecutable', $previousInstalled) } finally { $record.Dispose() }
}
if ($Case -in @('TrayUpdate', 'TrayCleanupFailure')) {
    $installedExecutable = Join-Path $global:testData 'RunnerDuck/TrayApp/RunnerDuck.exe'
    $retiredExecutable = Join-Path $global:fixture 'Legacy build/RunnerDuck.exe'
    $global:running = [pscustomobject]@{ Path = $retiredExecutable }
    $global:running | Add-Member ScriptMethod WaitForExit { param($timeout); return $true }
    $run = $global:testRegistryRoot.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try { $run.SetValue('RunnerDuck', ('"{0}"' -f $installedExecutable)) } finally { $run.Dispose() }
    foreach ($name in @('installed', 'retired', 'unrelated')) {
        $entry = $global:testRegistryRoot.CreateSubKey("Control Panel\NotifyIconSettings\$name")
        try {
            $entry.SetValue('ExecutablePath', $(switch ($name) {
                'installed' { $installedExecutable }; 'retired' { $retiredExecutable }; default { 'C:\Other\App.exe' }
            }))
            $entry.SetValue('IsPromoted', 1)
        } finally { $entry.Dispose() }
    }
    if ($Case -eq 'TrayCleanupFailure') {
        $null = New-Item -ItemType Directory -Path (Join-Path $global:testData 'RunnerDuck') -Force
        Set-Content -LiteralPath (Join-Path $global:testData 'RunnerDuck/TrayRegistrationBackups') 'block backup directory creation'
    }
}
function New-TestPackage {
    param([string]$Location)
    return [pscustomobject]@{ IsDevelopmentMode = $true; Publisher = 'CN=duks93'; InstallLocation = $Location;
        Status = 'Ok'; Version = '1.0.0.0'; PackageFamilyName = 'RunnerDuck_test'; PackageFullName = 'RunnerDuck_test_1' }
}
if ($Case -in @('PackageToTray', 'RegistryWriteFailure')) {
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
if ($Case -eq 'InvalidPayload') { $manifest.Package.Identity.SetAttribute('Name', 'InvalidFixture') }
$manifest.Save((Join-Path $payload 'AppxManifest.xml'))
$runnerAssets = foreach ($runner in @('penguin', 'chicken', 'dinosaur', 'owl', 'cat', 'dog', 'pig', 'sheep', 'fox', 'turtle')) { "Runners/$runner.json"; "Runners/$runner.png" }
foreach ($relative in @('RunnerDuck.exe', 'RunnerDuck.dll', 'RunnerDuck.deps.json', 'RunnerDuck.runtimeconfig.json',
    'coreclr.dll', 'hostfxr.dll', 'Microsoft.WindowsAppRuntime.dll', 'resources.pri', 'ko/RunnerDuck.resources.dll',
    'Assets/StoreLogo.png', 'Assets/Square150x150Logo.png', 'Assets/Square44x44Logo.png',
    'ProviderAssets/AIUsage_Icon.png', 'ProviderAssets/AIUsage_Screenshot.png',
    'Duck/animation.json', 'Duck/duck-atlas.png', 'Duck/duck-filled-atlas.png',
    'Legal/NOTICE.txt', 'Legal/LICENSE.txt', 'Legal/THIRD_PARTY.txt') + $runnerAssets) {
    $path = Join-Path $payload $relative
    $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
    Set-Content -LiteralPath $path 'fixture'
}
function Import-Module { param($Name) }
function Get-Process { param($Name, $ErrorAction); if ($null -ne $global:running) { return $global:running } }
function Stop-Process { param($InputObject, [switch]$Force); $global:stopped = $true }
function Invoke-WebRequest { throw 'Offline install attempted a download.' }
function dotnet { throw 'Offline install attempted to use an SDK.' }
function Get-ItemPropertyValue {
    param($Path, $Name, $ErrorAction)
    if ($global:testMode -eq 'Tray') { throw 'Tray touched Developer Mode.' }
    return [int]$global:developerEnabled
}
function Start-Process {
    param($FilePath, $ArgumentList, $Verb, $WindowStyle, [switch]$PassThru, [switch]$Wait)
    $launchDirectory = if ($global:testMode -eq 'Tray') { Join-Path $global:testData 'RunnerDuck/TrayApp' } elseif ($null -ne $global:package) { $global:package.InstallLocation } else { '' }
    $expectedExecutable = if ($launchDirectory) { Join-Path $launchDirectory 'RunnerDuck.exe' } else { '' }
    if (($global:testMode -eq 'Tray' -and $FilePath -eq $expectedExecutable) -or $FilePath -like 'shell:AppsFolder*') {
        $record = $global:testRegistryRoot.OpenSubKey('Software\RunnerDuck')
        try {
            if ($null -eq $record -or $record.GetValue('InstalledExecutable') -cne $expectedExecutable) {
                throw 'App launched before the installed location was recorded.'
            }
        } finally { if ($null -ne $record) { $record.Dispose() } }
        if ($WindowStyle -ne 'Normal') { throw 'GUI app launch must allow settings windows to appear.' }
        $global:launched = $expectedExecutable
        $global:running = [pscustomobject]@{ Path = $expectedExecutable }
        $global:running | Add-Member ScriptMethod WaitForExit { param($timeout); return $true }
        return
    }
    if ($Verb -ne 'RunAs' -or $global:testMode -eq 'Tray') { throw 'Unexpected elevation or launch.' }
    $global:elevations++
    $global:developerEnabled = $true
    return [pscustomobject]@{ ExitCode = 0 }
}
function Start-Sleep { param($Seconds) }
function Write-Warning {
    param($Message)
    $global:cleanupWarnings += $Message
    Microsoft.PowerShell.Utility\Write-Warning $Message
}
function Get-AppxPackage { param($Name); return $global:package }
function Add-AppxPackage {
    param($Register)
    if ($global:testMode -eq 'Tray') { throw 'Tray registered a package.' }
    $global:registrations++
    $global:package = New-TestPackage (Split-Path $Register)
    if ($Case -eq 'PackageVerificationFailure') { $global:package.Status = 'Error' }
}
function Remove-AppxPackage {
    param($Package)
    $record = $global:testRegistryRoot.OpenSubKey('Software\RunnerDuck')
    try {
        if ($record.GetValue('InstalledExecutable') -ne (Join-Path $global:testData 'RunnerDuck/TrayApp/RunnerDuck.exe')) {
            throw 'Package removed before the Tray location was recorded.'
        }
    } finally { $record.Dispose() }
    $global:removals++
    $global:package = $null
}
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
$parameters = @{ Platform = 'x64'; NoLaunch = ($mode -eq 'Widget') }
if ($Case -ne 'Tray') { $parameters.InstallMode = $mode }
if ($Case -in @('RegistryWriteFailure', 'PackageRegistryWriteFailure')) {
    $global:testRegistryRoot.Dispose()
    $global:testRegistryRoot = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($testRegistryPath, $false)
}
& (Join-Path $windows 'Install-RunnerDuck.ps1') @parameters
if ($expectedFailure) {
    if ($LASTEXITCODE -eq 0) { throw 'Expected installation to fail.' }
    if ($null -ne $global:launched) { throw 'Failed installation launched an app.' }
    if ($global:removals -ne 0) { throw 'Failed installation removed the previous package.' }
    $record = $global:testRegistryRoot.OpenSubKey('Software\RunnerDuck')
    try {
        if ($record.GetValue('InstalledExecutable') -cne $previousInstalled) { throw 'Failed installation changed the previous record.' }
    } finally { $record.Dispose() }
    Write-Host "PASS: $Case (expected failure)"
    return
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$destination = if ($mode -eq 'Tray') { Join-Path $global:testData 'RunnerDuck/TrayApp' } else { $global:package.InstallLocation }
$record = $global:testRegistryRoot.OpenSubKey('Software\RunnerDuck')
try {
    if ($record.GetValue('InstalledExecutable') -cne (Join-Path $destination 'RunnerDuck.exe') -or
        $record.GetValueKind('InstalledExecutable') -ne [Microsoft.Win32.RegistryValueKind]::String) { throw 'Wrong installed location record.' }
} finally { $record.Dispose() }
if ((Get-Content (Join-Path $destination 'install-mode.txt') -Raw).Trim() -ne $mode) { throw 'Wrong saved mode.' }
if ($global:elevations -ne [int]($mode -ne 'Tray')) { throw 'Wrong elevation count.' }
if ($global:registrations -ne [int]($mode -ne 'Tray')) { throw 'Wrong registration count.' }
if ($global:removals -ne [int]($Case -eq 'PackageToTray')) { throw 'Wrong removal count.' }
if ($Case -eq 'Tray' -and $global:reads -ne 2) { throw 'Invalid menu input was not retried.' }
if ($Case -eq 'PackageToTray' -and -not (Test-Path (Join-Path $global:testData 'RunnerDuck/settings.json'))) { throw 'Settings lost switching to Tray.' }
if ($Case -eq 'TrayToBoth' -and -not (Test-Path (Join-Path $global:testData 'Packages/RunnerDuck_test/LocalState/settings.json'))) { throw 'Settings lost switching to widgets.' }
if ($Case -in @('TrayUpdate', 'TrayCleanupFailure')) {
    if (-not $global:stopped) { throw 'Previous build was not stopped.' }
    $tray = $global:testRegistryRoot.OpenSubKey('Control Panel\NotifyIconSettings')
    $run = $global:testRegistryRoot.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        $expectedEntries = if ($Case -eq 'TrayCleanupFailure') { 'installed,retired,unrelated' } else { 'installed,unrelated' }
        if (($tray.GetSubKeyNames() | Sort-Object) -join ',' -ne $expectedEntries) { throw 'Retired tray cleanup scope mismatch.' }
        if ($run.GetValue('RunnerDuck') -cne ('"{0}"' -f $installedExecutable)) { throw 'Canonical startup command changed.' }
        if ($Case -eq 'TrayCleanupFailure') {
            if ($global:cleanupWarnings.Count -ne 1 -or $global:cleanupWarnings[0] -notlike '*tray registration repair failed*') { throw 'Cleanup failure was not reported.' }
            if ($global:launched -ne $installedExecutable) { throw 'Cleanup failure prevented launching the installed app.' }
        } elseif (@(Get-ChildItem -LiteralPath (Join-Path $global:testData 'RunnerDuck/TrayRegistrationBackups')).Count -ne 1) {
            throw 'Missing tray backup.'
        }
    } finally { $run.Dispose(); $tray.Dispose() }
}
if ($Case -eq 'TrayUpdate') {
    & (Join-Path $windows 'Install-RunnerDuck.ps1') @parameters
    if ($LASTEXITCODE -ne 0) { throw 'Repeated Tray update failed.' }
    $tray = $global:testRegistryRoot.OpenSubKey('Control Panel\NotifyIconSettings')
    try {
        if (($tray.GetSubKeyNames() | Sort-Object) -join ',' -ne 'installed,unrelated') { throw 'Repeated update changed tray history.' }
    } finally { $tray.Dispose() }
    if (@(Get-ChildItem -LiteralPath (Join-Path $global:testData 'RunnerDuck/TrayRegistrationBackups')).Count -ne 1) { throw 'Repeated update was not idempotent.' }
}
Write-Host "PASS: $Case"
} finally {
    $global:testRegistryRoot.Dispose()
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($testRegistryPath)
}
