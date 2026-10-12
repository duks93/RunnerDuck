#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerComponents.ps1')
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$trayPath = 'Control Panel\NotifyIconSettings'
$oldExecutable = 'C:\RunnerDuck build\RunnerDuck.exe'
$otherExecutable = 'C:\Other\RunnerDuck.exe'

function Add-TestTrayEntry {
    param($Root, [string]$Name, [string]$Executable, [int]$Promoted = 0)
    $entry = $Root.CreateSubKey("$trayPath\$Name")
    try {
        $entry.SetValue('ExecutablePath', $Executable)
        $entry.SetValue('InitialTooltip', 'RunnerDuck')
        $entry.SetValue('IsPromoted', $Promoted)
        $entry.SetValue('IconSnapshot', [byte[]]@(0, 1, 255))
    } finally { $entry.Dispose() }
}

function Invoke-TrayCase {
    param([string]$Name, [scriptblock]$Check)
    $testKey = 'Software\RunnerDuck.Tests\TrayRegistration-' + [guid]::NewGuid().ToString('N')
    $root = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($testKey)
    $directory = Join-Path $PSScriptRoot ("../artifacts/tray-registration-tests/{0}-{1}" -f $Name, [guid]::NewGuid())
    $null = New-Item -ItemType Directory -Path $directory -Force
    $installed = [IO.Path]::GetFullPath((Join-Path $directory 'RunnerDuck.exe'))
    Set-Content -LiteralPath $installed 'fixture'
    $parameters = @{ CurrentUser = $root; InstalledExecutable = $installed; PreviousExecutables = @($oldExecutable);
        BackupDirectory = (Join-Path $directory 'backups') }
    try {
        & $Check $root $parameters
        Write-Host "PASS: $Name"
    } finally {
        $root.Dispose()
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($testKey)
    }
}

Invoke-TrayCase 'repair-and-backup' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    $tray = $root.CreateSubKey($trayPath)
    $approval = $root.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run')
    try {
        $run.SetValue('RunnerDuck', ('"{0}"' -f $oldExecutable))
        $run.SetValue('OtherApp', 'keep-me')
        $approval.SetValue('RunnerDuck', [byte[]]@(3, 0, 0, 0))
        Add-TestTrayEntry $root 'retired' $oldExecutable
        Add-TestTrayEntry $root 'installed' $parameters.InstalledExecutable 1
        Add-TestTrayEntry $root 'unrelated-runner' $otherExecutable
        Add-TestTrayEntry $root 'different-app' 'C:\Other\App.exe'
        $result = Repair-RunnerDuckTrayRegistration @parameters
        if (-not $result.UpdatedStartup -or $result.RemovedEntries -ne 1) { throw 'Repair did not select exactly one retired entry.' }
        if ($run.GetValue('RunnerDuck') -cne ('"{0}"' -f $parameters.InstalledExecutable)) { throw 'Startup still targets the old executable.' }
        if ($run.GetValue('OtherApp') -cne 'keep-me') { throw 'Another startup command changed.' }
        if ([Convert]::ToBase64String($approval.GetValue('RunnerDuck')) -ne 'AwAAAA==') { throw 'Windows startup opt-out changed.' }
        if (($tray.GetSubKeyNames() | Sort-Object) -join ',' -ne 'different-app,installed,unrelated-runner') { throw 'Unrelated tray entries changed.' }
        $installedEntry = $tray.OpenSubKey('installed')
        try { if ($installedEntry.GetValue('IsPromoted') -ne 1) { throw 'Installed visibility changed.' } }
        finally { $installedEntry.Dispose() }
        $backup = Import-Clixml -LiteralPath $result.BackupPath
        if ($backup.Entries.Count -ne 1 -or $backup.Entries[0].Key -cne 'retired') { throw 'Incorrect backup scope.' }
        $pixels = @($backup.Entries[0].Values | Where-Object { $_.Name -eq 'IconSnapshot' })[0]
        if ($pixels.Kind -cne 'Binary' -or [Convert]::ToBase64String($pixels.Value) -cne 'AAH/') { throw 'Backup lost registry value types or bytes.' }
        if ($backup.StartupCommand -cne ('"{0}"' -f $oldExecutable) -or $backup.StartupKind -cne 'String') { throw 'Startup backup missing.' }
        $second = Repair-RunnerDuckTrayRegistration @parameters
        if ($second.UpdatedStartup -or $second.RemovedEntries -ne 0 -or $second.BackupPath) { throw 'Repair is not idempotent.' }
    } finally { $approval.Dispose(); $tray.Dispose(); $run.Dispose() }
}

Invoke-TrayCase 'startup-only-migration' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    try {
        $run.SetValue('RunnerDuck', ('"{0}"' -f $otherExecutable))
        $parameters.PreviousExecutables = @()
        $result = Repair-RunnerDuckTrayRegistration @parameters
        if (-not $result.UpdatedStartup -or $result.RemovedEntries -ne 0 -or -not $result.BackupPath) { throw 'Startup-only migration failed.' }
    } finally { $run.Dispose() }
}

Invoke-TrayCase 'disabled-startup' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    $preferences = $root.CreateSubKey('Software\RunnerDuck')
    try {
        $preferences.SetValue('LaunchAtLoginInitialized', 1)
        Add-TestTrayEntry $root 'retired' $oldExecutable
        $result = Repair-RunnerDuckTrayRegistration @parameters
        if ($result.UpdatedStartup -or $null -ne $run.GetValue('RunnerDuck')) { throw 'Removed startup entry was recreated.' }
        if ($preferences.GetValue('LaunchAtLoginInitialized') -ne 1) { throw 'Initialization preference changed.' }
        if ($result.RemovedEntries -ne 1) { throw 'Disabled startup prevented tray cleanup.' }
    } finally { $preferences.Dispose(); $run.Dispose() }
}

Invoke-TrayCase 'fresh-install' {
    param($root, $parameters)
    $result = Repair-RunnerDuckTrayRegistration @parameters
    if ($result.UpdatedStartup -or $result.RemovedEntries -ne 0 -or $result.BackupPath -or $root.SubKeyCount -ne 0) { throw 'Fresh install unexpectedly changed registry state.' }
}

Invoke-TrayCase 'unrecognized-command-and-paths' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    $tray = $root.CreateSubKey($trayPath)
    try {
        $command = '"C:\RunnerDuck build\RunnerDuck.exe" --custom-option'
        $run.SetValue('RunnerDuck', $command)
        Add-TestTrayEntry $root 'unrelated' 'C:\Other\App.exe'
        Add-TestTrayEntry $root 'installed' $parameters.InstalledExecutable 1
        $parameters.PreviousExecutables = @('C:\Other\App.exe', 'RunnerDuck.exe', $parameters.InstalledExecutable.ToUpperInvariant())
        $result = Repair-RunnerDuckTrayRegistration @parameters
        if ($result.RemovedEntries -ne 0 -or $result.UpdatedStartup -or $run.GetValue('RunnerDuck') -cne $command) { throw 'Unrecognized command or unrelated executable changed.' }
        if ($tray.SubKeyCount -ne 2) { throw 'Installed or unrelated entry was deleted.' }
    } finally { $tray.Dispose(); $run.Dispose() }
}

Invoke-TrayCase 'case-insensitive-retired-path' {
    param($root, $parameters)
    Add-TestTrayEntry $root 'retired' $oldExecutable.ToUpperInvariant()
    $result = Repair-RunnerDuckTrayRegistration @parameters
    if ($result.RemovedEntries -ne 1) { throw 'Windows path casing prevented cleanup.' }
}

Invoke-TrayCase 'backup-failure-is-safe' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    $tray = $root.CreateSubKey($trayPath)
    try {
        $command = '"{0}"' -f $oldExecutable
        $run.SetValue('RunnerDuck', $command)
        Add-TestTrayEntry $root 'retired' $oldExecutable
        $parameters.BackupDirectory = $parameters.InstalledExecutable
        $failed = $false
        try { $null = Repair-RunnerDuckTrayRegistration @parameters } catch { $failed = $true }
        if (-not $failed -or $tray.SubKeyCount -ne 1 -or $run.GetValue('RunnerDuck') -cne $command) { throw 'Backup failure modified live registrations.' }
    } finally { $tray.Dispose(); $run.Dispose() }
}

Invoke-TrayCase 'missing-installation-is-safe' {
    param($root, $parameters)
    Add-TestTrayEntry $root 'retired' $oldExecutable
    $parameters.InstalledExecutable = Join-Path (Split-Path $parameters.InstalledExecutable) 'missing/RunnerDuck.exe'
    $failed = $false
    try { $null = Repair-RunnerDuckTrayRegistration @parameters } catch { $failed = $true }
    $tray = $root.OpenSubKey($trayPath)
    try { if (-not $failed -or $tray.SubKeyCount -ne 1) { throw 'Missing installation removed history.' } }
    finally { $tray.Dispose() }
}

Invoke-TrayCase 'expandable-startup-backup' {
    param($root, $parameters)
    $run = $root.CreateSubKey($runPath)
    try {
        $command = '"%TEMP%\old\RunnerDuck.exe"'
        $run.SetValue('RunnerDuck', $command, [Microsoft.Win32.RegistryValueKind]::ExpandString)
        $result = Repair-RunnerDuckTrayRegistration @parameters
        $backup = Import-Clixml -LiteralPath $result.BackupPath
        if (-not $result.UpdatedStartup -or $backup.StartupCommand -cne $command -or $backup.StartupKind -cne 'ExpandString') {
            throw 'Expandable startup command was not preserved in the backup.'
        }
    } finally { $run.Dispose() }
}

foreach ($invalidPath in @('RunnerDuck.exe', 'C:RunnerDuck.exe', '\RunnerDuck.exe', 'C:\build\RunnerDuck.exe" --arg C:\other\RunnerDuck.exe')) {
    if ($null -ne (Get-RunnerDuckExecutablePath $invalidPath)) { throw "Unsafe executable path accepted: $invalidPath" }
}
Write-Host 'Tray registration checks passed (9 cases plus invalid paths).'
