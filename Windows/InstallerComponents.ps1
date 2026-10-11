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
