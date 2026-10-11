function Enable-RunnerDuckDeveloperMode {
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
    if ((Get-ItemPropertyValue -Path $key -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue) -eq 1) { return }
    Write-Host '위젯 설치를 위해 개발자 모드를 켭니다. Windows 관리자 권한 요청을 승인해 주세요.'
    # Elevate only this fixed operation, never the build or the per-user installation.
    $command = @'
$ErrorActionPreference = 'Stop'
try {
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
    $null = New-Item -Path $key -Force
    $null = New-ItemProperty -Path $key -Name AllowDevelopmentWithoutDevLicense -PropertyType DWord -Value 1 -Force
    exit 0
} catch { exit 1 }
'@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $process = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
        -ArgumentList '-NoProfile', '-EncodedCommand', $encoded -Verb RunAs -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or (Get-ItemPropertyValue -Path $key -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue) -ne 1) {
        throw 'Windows did not enable Developer Mode. Administrator approval is required for widgets; Tray mode does not need it.'
    }
}

function Resolve-RunnerDuckDotNet {
    param([Parameter(Mandatory)][string]$Platform)
    $sdkDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RunnerDuck/dotnet'
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    $candidates = @((Join-Path $sdkDirectory 'dotnet.exe'))
    if ($null -ne $installed) { $candidates += $installed.Source }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            $sdks = & $candidate --list-sdks
            if ($LASTEXITCODE -eq 0 -and ($sdks -match '^10\.')) { return $candidate }
        }
    }
    Write-Host '.NET 10 SDK를 자동으로 준비합니다 / Preparing .NET 10 SDK...'
    $download = Join-Path ([IO.Path]::GetTempPath()) ("RunnerDuck-dotnet-{0}.ps1" -f [guid]::NewGuid())
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $download
        & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass `
            -File $download -Channel 10.0 -InstallDir $sdkDirectory -Architecture $Platform.ToLowerInvariant() -NoPath | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '.NET SDK setup failed.' }
    } catch {
        throw "Could not prepare the SDK. Connect to the internet or use the offline ZIP. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force }
    }
    $dotnet = Join-Path $sdkDirectory 'dotnet.exe'
    $sdks = & $dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) { throw '.NET 10 SDK setup could not be verified.' }
    return $dotnet
}

function Copy-RunnerDuckSettings {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path -LiteralPath $Source)) { return }
    $null = New-Item -ItemType Directory -Path $Destination -Force
    foreach ($name in @('settings.json', 'customRunners.json', 'CustomRunners', 'ai-usage-snapshot.json')) {
        $path = Join-Path $Source $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $Destination -Recurse -Force }
    }
}
