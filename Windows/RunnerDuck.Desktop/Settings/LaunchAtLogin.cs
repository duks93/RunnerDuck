/*
 LaunchAtLogin.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
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

// Adapted from RunCat365 (Apache License 2.0): https://github.com/runcat-dev/RunCat365

using RunnerDuck.Core;
using Windows.ApplicationModel;
using Microsoft.Win32;

namespace RunnerDuck.App.Settings;

internal static class LaunchAtLogin
{
    private const string TaskID = "RunnerDuckStartup";
    private static readonly TrayLaunchAtLogin Tray = new(Registry.CurrentUser);

    private static bool IsPackaged
    {
        get
        {
            try { return !string.IsNullOrEmpty(Package.Current.Id.Name); }
            catch (InvalidOperationException) { return false; }
            catch (System.Runtime.InteropServices.COMException) { return false; }
        }
    }

    public static void EnableByDefault()
    {
        try
        {
            if (!IsPackaged) Tray.EnableByDefault(Environment.ProcessPath!);
        }
        catch (Exception exception)
        {
            Log.Write($"Enabling launch at login by default failed: {exception.Message}");
        }
    }

    public static async Task<LaunchAtLoginState> StateAsync()
    {
        try
        {
            if (!IsPackaged)
            {
                return Tray.IsEnabled ? LaunchAtLoginState.Enabled : LaunchAtLoginState.Disabled;
            }
            var task = await StartupTask.GetAsync(TaskID);
            return Map(task.State);
        }
        catch (Exception exception)
        {
            Log.Write($"Reading the startup task failed: {exception.Message}");
            return LaunchAtLoginState.Unavailable;
        }
    }

    public static async Task<LaunchAtLoginState> SetAsync(bool isOn)
    {
        try
        {
            if (!IsPackaged)
            {
                Tray.Set(isOn, Environment.ProcessPath!);
                return await StateAsync();
            }
            var task = await StartupTask.GetAsync(TaskID);
            if (isOn)
            {
                return Map(await task.RequestEnableAsync());
            }
            task.Disable();
            return Map(task.State);
        }
        catch (Exception exception)
        {
            Log.Write($"Changing the startup task failed: {exception.Message}");
            return await StateAsync();
        }
    }

    private static LaunchAtLoginState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => LaunchAtLoginState.Enabled,
        StartupTaskState.Disabled => LaunchAtLoginState.Disabled,
        StartupTaskState.DisabledByUser => LaunchAtLoginState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => LaunchAtLoginState.DisabledByPolicy,
        StartupTaskState.EnabledByPolicy => LaunchAtLoginState.EnabledByPolicy,
        _ => LaunchAtLoginState.Unavailable,
    };
}
