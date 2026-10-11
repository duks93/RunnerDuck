<#
.SYNOPSIS
Builds, installs, and launches Release RunnerDuck for the current user.
.DESCRIPTION
Requires PowerShell 5.1 or later. Missing build tools are prepared automatically.
Only widget modes enable Developer Mode (Windows administrator approval required).
Builds the native x64 or ARM64 app with Windows App SDK dependencies included.
Updates use the existing development package's registered location so settings,
package identity, and pinned widget registrations remain associated with the app.
New installations use %LOCALAPPDATA%\RunnerDuck\App. No certificate is required.
.PARAMETER NoLaunch
Install without launching the tray app after success.
.PARAMETER InstallMode
Both, Widget, or Tray. Omit to choose interactively in the terminal.
.EXAMPLE
pwsh -File install.ps1
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform,
    [ValidateSet('Both', 'Widget', 'Tray')]
    [string]$InstallMode,
    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$packageName = 'RunnerDuck'
$projectPath = Join-Path $PSScriptRoot 'RunnerDuck.Desktop/RunnerDuck.App.csproj'
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$wasRunning = $false
$offlinePayload = Join-Path $PSScriptRoot 'Payload'
$isOfflineBundle = Test-Path -LiteralPath $offlinePayload -PathType Container
$appRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RunnerDuck'
$trayDirectory = Join-Path $appRoot 'TrayApp'
$trayShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'RunnerDuck.lnk'
$installDirectory = $null
. (Join-Path $PSScriptRoot 'InstallerComponents.ps1')
. (Join-Path $PSScriptRoot 'InstallerPrerequisites.ps1')

function Start-InstalledRunnerDuck {
    if ($InstallMode -eq 'Tray' -and $null -ne $installDirectory) {
        Start-Process -FilePath (Join-Path $installDirectory 'RunnerDuck.exe') -WindowStyle Hidden
        return
    }
    $current = @(Get-AppxPackage -Name $packageName)
    if ($current.Count -eq 1) {
        Start-Process -WindowStyle Hidden "shell:AppsFolder\$($current[0].PackageFamilyName)!App"
    }
}

try {
    if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'Run install.sh on macOS.' }
    if ([string]::IsNullOrEmpty($InstallMode)) {
        Write-Host ''
        Write-Host 'RunnerDuck 설치 구성 / Installation components'
        Write-Host '  1. 위젯 + 트레이 / Widget + Tray (Both)'
        Write-Host '  2. 위젯만 / Widget only (Widget)'
        Write-Host '  3. 트레이만 / Tray only (Tray)'
        do {
            $choice = Read-Host '번호를 입력하세요 / Select [1-3]'
            $selectedMode = switch ($choice.Trim()) {
                '1' { 'Both' }
                '2' { 'Widget' }
                '3' { 'Tray' }
                default { '' }
            }
            if (-not $selectedMode) { Write-Host '1, 2, 3 중 하나를 입력하세요 / Enter 1, 2, or 3.' }
        } while (-not $selectedMode)
        $InstallMode = $selectedMode
    }
    Write-Host "Selected components: $InstallMode"
    if ($InstallMode -eq 'Tray') {
        Write-Host 'Widget registration will be removed; previously pinned widgets may need to be added again if re-enabled.'
    }
    if ([string]::IsNullOrEmpty($Platform)) {
        $Platform = switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
            'X64' { 'x64' }
            'Arm64' { 'ARM64' }
            default { throw 'RunnerDuck requires x64 or ARM64 Windows.' }
        }
    }
    if ($InstallMode -ne 'Tray') {
        if ([Environment]::OSVersion.Version.Build -lt 22000) { throw 'Widgets require Windows 11. Select Tray on Windows 10.' }
        Enable-RunnerDuckDeveloperMode
    }
    if (-not $isOfflineBundle) { $dotnet = Resolve-RunnerDuckDotNet -Platform $Platform }
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue
    } else {
        Import-Module Appx
    }
    $previousPackages = @(Get-AppxPackage -Name $packageName)
    if ($previousPackages.Count -gt 1) { throw 'More than one RunnerDuck package is registered.' }
    $previousPackage = if ($previousPackages.Count -eq 1) { $previousPackages[0] } else { $null }
    if ($null -ne $previousPackage -and (-not $previousPackage.IsDevelopmentMode -or $previousPackage.Publisher -ne 'CN=duks93')) {
        throw 'This source installer can update only the RunnerDuck development package.'
    }

    $running = @(Get-Process -Name RunnerDuck -ErrorAction SilentlyContinue)
    $wasRunning = $running.Count -gt 0
    Write-Host 'Stopping RunnerDuck for the update...'
    foreach ($appProcess in $running) {
        Stop-Process -InputObject $appProcess -Force
        if (-not $appProcess.WaitForExit(10000)) { throw "Process $($appProcess.Id) did not exit." }
    }

    if ($isOfflineBundle) {
        Write-Host 'Installing the bundled app offline (no SDK or downloads required)...'
        $layoutPath = $offlinePayload
        foreach ($runtimeFile in @('coreclr.dll', 'hostfxr.dll', 'Microsoft.WindowsAppRuntime.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $layoutPath $runtimeFile))) {
                throw "Offline bundle is missing $runtimeFile. Recreate the bundle on the build PC."
            }
        }
    } else {
        Write-Host "Building RunnerDuck ($Platform Release)..."
        $buildProperties = @('-p:Configuration=Release', "-p:Platform=$Platform",
            "-p:RuntimeIdentifier=win-$($Platform.ToLowerInvariant())", '-p:SelfContained=true', '-p:WindowsAppSDKSelfContained=true')
        & $dotnet build $projectPath -t:Rebuild @buildProperties
        if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
        $targetOutput = & $dotnet msbuild $projectPath -nologo @buildProperties -getProperty:TargetDir
        if ($LASTEXITCODE -ne 0) { throw 'MSBuild could not resolve the build output.' }
        $layoutPath = ($targetOutput -join [Environment]::NewLine).Trim()
        if ([string]::IsNullOrWhiteSpace($layoutPath)) { throw 'MSBuild returned an empty build output path.' }
    }
    $layoutPath = [IO.Path]::GetFullPath($layoutPath).TrimEnd('\')
    $manifestPath = Join-Path $layoutPath 'AppxManifest.xml'
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    if ($manifest.Package.Identity.Name -ne $packageName -or
        $manifest.Package.Identity.Publisher -ne 'CN=duks93' -or
        $manifest.Package.Identity.ProcessorArchitecture -ne $Platform.ToLowerInvariant()) {
        throw 'The generated manifest does not match RunnerDuck and the selected architecture.'
    }
    foreach ($relativePath in @(
        'RunnerDuck.exe', 'RunnerDuck.dll', 'RunnerDuck.deps.json', 'RunnerDuck.runtimeconfig.json',
        'resources.pri', 'ko/RunnerDuck.resources.dll',
        'Assets/StoreLogo.png', 'Assets/Square150x150Logo.png', 'Assets/Square44x44Logo.png',
        'ProviderAssets/AIUsage_Icon.png', 'ProviderAssets/AIUsage_Screenshot.png',
        'Duck/animation.json', 'Duck/duck-atlas.png', 'Duck/duck-filled-atlas.png',
        'Legal/NOTICE.txt', 'Legal/LICENSE.txt', 'Legal/THIRD_PARTY.txt'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $layoutPath $relativePath) -PathType Leaf)) {
            throw "Build output is missing $relativePath."
        }
    }

    # Keep the registered location: changing it at the same package version can
    # silently retain the old location. Unregistering would also disturb widgets.
    $installDirectory = if ($InstallMode -eq 'Tray') {
        $trayDirectory
    } elseif ($null -ne $previousPackage) {
        $previousPackage.InstallLocation
    } else {
        Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RunnerDuck/App'
    }
    $installDirectory = [IO.Path]::GetFullPath($installDirectory).TrimEnd('\')
    if ($installDirectory -eq [IO.Path]::GetPathRoot($installDirectory).TrimEnd('\') -or
        $installDirectory -eq $repositoryPath -or
        (Test-Path -LiteralPath (Join-Path $installDirectory '.git'))) {
        throw 'The install location must be a dedicated app directory.'
    }
    if ($installDirectory -ne $layoutPath) {
        $null = New-Item -ItemType Directory -Path $installDirectory -Force
        if ((Get-Item -LiteralPath $installDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The install directory must not be a symbolic link.'
        }
        Write-Host "Installing to $installDirectory"
        foreach ($entry in Get-ChildItem -LiteralPath $layoutPath -Force) {
            Copy-Item -LiteralPath $entry.FullName -Destination $installDirectory -Recurse -Force
        }
    }
    $installedManifestPath = Join-Path $installDirectory 'AppxManifest.xml'
    Set-RunnerDuckInstallMode -Manifest $manifest -InstallMode $InstallMode
    $manifest.Save($installedManifestPath)
    Set-Content -LiteralPath (Join-Path $installDirectory 'install-mode.txt') -Value $InstallMode -Encoding utf8
    if ($InstallMode -eq 'Tray') {
        if ($null -ne $previousPackage) {
            $oldData = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "Packages/$($previousPackage.PackageFamilyName)/LocalState"
            Copy-RunnerDuckSettings -Source $oldData -Destination $appRoot
            Remove-AppxPackage -Package $previousPackage.PackageFullName
        }
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($trayShortcut)
        $shortcut.TargetPath = Join-Path $installDirectory 'RunnerDuck.exe'
        $shortcut.WorkingDirectory = $installDirectory
        $shortcut.Save()
        Write-Host "Installed RunnerDuck Tray (no Developer Mode required): $installDirectory"
    } else {
    Add-AppxPackage -Register $installedManifestPath
    $registered = @(Get-AppxPackage -Name $packageName)
    if ($registered.Count -ne 1) { throw 'RunnerDuck package registration was not completed.' }
    $package = $registered[0]
    if ($package.Status -ne 'Ok' -or
        [version]$package.Version -ne [version]$manifest.Package.Identity.Version -or
        $package.InstallLocation.TrimEnd('\') -ne $installDirectory) {
        throw 'The registered package does not match the new build.'
    }
    Write-Host "Installed RunnerDuck $($package.Version): $installDirectory"
    if ($null -eq $previousPackage -and (Test-Path -LiteralPath (Join-Path $trayDirectory 'RunnerDuck.exe'))) {
        $packageData = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "Packages/$($package.PackageFamilyName)/LocalState"
        Copy-RunnerDuckSettings -Source $appRoot -Destination $packageData
    }
    if (Test-Path -LiteralPath $trayShortcut) { Remove-Item -LiteralPath $trayShortcut -Force }
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name RunnerDuck -ErrorAction SilentlyContinue
    }
    if ($InstallMode -ne 'Tray') {
        Write-Host '위젯 추가: Win+W → 위젯 갤러리 → RunnerDuck / AI Usage'
        Write-Host 'Add the widget from the Windows Widgets board (Win+W).'
    }
    if (-not $NoLaunch -and $InstallMode -ne 'Widget') {
        Start-InstalledRunnerDuck
        Start-Sleep -Seconds 3
        $running = @(Get-Process -Name RunnerDuck -ErrorAction SilentlyContinue | Where-Object {
            $_.Path -eq (Join-Path $installDirectory 'RunnerDuck.exe')
        })
        if ($running.Count -eq 0) { throw 'RunnerDuck exited after launch. Check App.log in the package LocalState directory.' }
        Write-Host 'RunnerDuck is running.'
    }
    exit 0
}
catch {
    if ($wasRunning) {
        try { Start-InstalledRunnerDuck } catch { Write-Warning 'Could not restart the installed app.' }
    }
    Write-Error "RunnerDuck installation failed: $($_.Exception.Message)" -ErrorAction Continue
    exit 1
}
