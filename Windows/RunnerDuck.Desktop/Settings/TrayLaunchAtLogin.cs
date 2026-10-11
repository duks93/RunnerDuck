/*
 TrayLaunchAtLogin.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/11.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

using Microsoft.Win32;

namespace RunnerDuck.App.Settings;

internal sealed class TrayLaunchAtLogin(RegistryKey currentUser)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string PreferencesKey = @"Software\RunnerDuck";
    private const string InitializedValue = "LaunchAtLoginInitialized";

    public bool IsEnabled
    {
        get
        {
            using var key = currentUser.OpenSubKey(RunKey);
            return key?.GetValue("RunnerDuck") is string;
        }
    }

    public void EnableByDefault(string executablePath)
    {
        using var preferences = currentUser.CreateSubKey(PreferencesKey);
        if (preferences.GetValue(InitializedValue) is not null) return;
        preferences.SetValue(InitializedValue, 1);
        if (!IsEnabled) Set(true, executablePath);
    }

    public void Set(bool isOn, string executablePath)
    {
        using var preferences = currentUser.CreateSubKey(PreferencesKey);
        preferences.SetValue(InitializedValue, 1);
        using var key = currentUser.CreateSubKey(RunKey);
        if (isOn)
            key.SetValue("RunnerDuck", $"\"{executablePath}\"");
        else
            key.DeleteValue("RunnerDuck", throwOnMissingValue: false);
    }
}
