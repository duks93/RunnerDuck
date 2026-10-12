function Set-RunnerDuckInstallMode {
    param(
        [Parameter(Mandatory)][xml]$Manifest,
        [Parameter(Mandatory)][ValidateSet('Both', 'Widget', 'Tray')][string]$InstallMode
    )

    foreach ($extension in @($Manifest.SelectNodes('//*[local-name()="Application"]/*[local-name()="Extensions"]/*'))) {
        $remove = ($InstallMode -eq 'Widget' -and $extension.GetAttribute('Category') -eq 'windows.startupTask') -or
            ($InstallMode -eq 'Tray' -and $extension.GetAttribute('Category') -in @('windows.comServer', 'windows.appExtension'))
        if ($remove) { $null = $extension.ParentNode.RemoveChild($extension) }
    }
}

function Get-RunnerDuckExecutablePath {
    param([AllowNull()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    try {
        if ($Path -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\]+\\[^\\]+\\)' -or
            $Path.IndexOfAny([char[]]'"<>|?*') -ge 0 -or $Path.Substring(2).Contains(':') -or
            [IO.Path]::GetFileName($Path) -ine 'RunnerDuck.exe') { return $null }
        return [IO.Path]::GetFullPath($Path)
    } catch { return $null }
}

function Set-RunnerDuckInstalledExecutable {
    param(
        [Parameter(Mandatory)][Microsoft.Win32.RegistryKey]$CurrentUser,
        [Parameter(Mandatory)][string]$InstalledExecutable
    )

    $installed = Get-RunnerDuckExecutablePath $InstalledExecutable
    if (-not $installed -or -not (Test-Path -LiteralPath $installed -PathType Leaf)) {
        throw 'The installed RunnerDuck executable must exist before recording its location.'
    }
    $key = $CurrentUser.CreateSubKey('Software\RunnerDuck')
    try {
        $key.SetValue('InstalledExecutable', $installed, [Microsoft.Win32.RegistryValueKind]::String)
        if ($key.GetValueKind('InstalledExecutable') -ne [Microsoft.Win32.RegistryValueKind]::String -or
            $key.GetValue('InstalledExecutable') -cne $installed) {
            throw 'The installed RunnerDuck location could not be verified.'
        }
    } finally { if ($null -ne $key) { $key.Dispose() } }
}

function Repair-RunnerDuckTrayRegistration {
    param(
        [Parameter(Mandatory)][Microsoft.Win32.RegistryKey]$CurrentUser,
        [Parameter(Mandatory)][string]$InstalledExecutable,
        [string[]]$PreviousExecutables = @(),
        [Parameter(Mandatory)][string]$BackupDirectory
    )

    $installed = Get-RunnerDuckExecutablePath $InstalledExecutable
    if (-not $installed -or -not (Test-Path -LiteralPath $installed -PathType Leaf)) {
        throw 'The installed RunnerDuck executable must exist before repairing registration.'
    }
    $runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
    $trayPath = 'Control Panel\NotifyIconSettings'
    $run = $CurrentUser.OpenSubKey($runPath, $true)
    $tray = $null
    try {
        $startupCommand = if ($null -ne $run) {
            $run.GetValue('RunnerDuck', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        } else { $null }
        $startupKind = if ($null -ne $startupCommand) { $run.GetValueKind('RunnerDuck') } else { $null }
        $startupExecutable = if ($startupCommand -is [string]) {
            $command = $startupCommand.Trim()
            if ($startupKind -eq [Microsoft.Win32.RegistryValueKind]::ExpandString) {
                $command = [Environment]::ExpandEnvironmentVariables($command)
            }
            if ($command.StartsWith('"') -and $command.EndsWith('"') -and $command.Length -ge 2) {
                $command = $command.Substring(1, $command.Length - 2)
            }
            Get-RunnerDuckExecutablePath $command
        } else { $null }
        $retired = @($PreviousExecutables) + @($startupExecutable)
        $retired = @($retired | ForEach-Object { Get-RunnerDuckExecutablePath $_ } |
            Where-Object { $_ -and $_ -ine $installed } | Select-Object -Unique)
        $tray = $CurrentUser.OpenSubKey($trayPath, $true)
        $entries = @()
        if ($null -ne $tray) {
            foreach ($name in $tray.GetSubKeyNames()) {
                $entry = $tray.OpenSubKey($name)
                try {
                    if ($null -eq $entry -or $entry.SubKeyCount -gt 0) { continue }
                    $executable = Get-RunnerDuckExecutablePath ($entry.GetValue('ExecutablePath') -as [string])
                    if (-not $executable -or $executable -notin $retired) { continue }
                    $values = @($entry.GetValueNames() | ForEach-Object {
                        [pscustomobject]@{
                            Name = $_
                            Kind = $entry.GetValueKind($_).ToString()
                            Value = $entry.GetValue($_, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                        }
                    })
                    $entries += [pscustomobject]@{ Key = $name; ExecutablePath = $executable; Values = $values }
                } finally { if ($null -ne $entry) { $entry.Dispose() } }
            }
        }
        $updateStartup = $startupExecutable -and $startupExecutable -ine $installed
        $backupPath = $null
        if ($entries.Count -gt 0 -or $updateStartup) {
            $null = New-Item -ItemType Directory -Path $BackupDirectory -Force -ErrorAction Stop
            $backupPath = Join-Path $BackupDirectory ("tray-registration-{0}.clixml" -f [guid]::NewGuid())
            [pscustomobject]@{
                TrayKey = $trayPath
                Entries = $entries
                StartupKey = $runPath
                StartupCommand = $startupCommand
                StartupKind = if ($null -ne $startupKind) { $startupKind.ToString() } else { $null }
            } | Export-Clixml -LiteralPath $backupPath -Depth 8 -Encoding UTF8 -ErrorAction Stop
        }
        if ($updateStartup) {
            $run.SetValue('RunnerDuck', ('"{0}"' -f $installed), [Microsoft.Win32.RegistryValueKind]::String)
        }
        foreach ($entry in $entries) {
            $tray.DeleteSubKey($entry.Key, $false)
        }
        return [pscustomobject]@{ UpdatedStartup = [bool]$updateStartup; RemovedEntries = $entries.Count; BackupPath = $backupPath }
    } finally {
        if ($null -ne $tray) { $tray.Dispose() }
        if ($null -ne $run) { $run.Dispose() }
    }
}
