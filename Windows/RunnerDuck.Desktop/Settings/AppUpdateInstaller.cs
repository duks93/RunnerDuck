/*
 AppUpdateInstaller.cs
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

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using RunnerDuck.Core;

namespace RunnerDuck.App.Settings;

internal static class AppUpdateInstaller
{
    private static string UpdateDirectory => Path.Combine(AppPaths.DataDirectory, "Update");

    public static async Task StartAsync(HttpClient httpClient, string commit, CancellationToken cancellationToken)
    {
        if (Directory.Exists(UpdateDirectory))
        {
            Directory.Delete(UpdateDirectory, true);
        }
        Directory.CreateDirectory(UpdateDirectory);
        var archivePath = Path.Combine(UpdateDirectory, "source.zip");
        using (var request = new HttpRequestMessage(HttpMethod.Get, AppUpdateInstallation.SourceArchiveURL(commit)))
        {
            request.Headers.UserAgent.ParseAdd("RunnerDuck");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(archivePath);
            await response.Content.CopyToAsync(file, cancellationToken);
        }
        ZipFile.ExtractToDirectory(archivePath, UpdateDirectory);
        File.Delete(archivePath);
        var sourceDirectory = AppUpdateInstallation.SourceDirectory(UpdateDirectory, commit);
        if (!File.Exists(Path.Combine(sourceDirectory, "Windows", "Install-RunnerDuck.ps1")))
        {
            throw new InvalidDataException("The downloaded source does not contain the RunnerDuck installer.");
        }
        var components = InstalledComponents.Load();
        var script = AppUpdateInstallation.Script(sourceDirectory, AppUpdateInstallation.InstallMode(components.Widget, components.Tray), commit);
        var scriptPath = Path.Combine(UpdateDirectory, AppUpdateInstallation.ScriptFileName);
        await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken);
        // Explorer starts the script outside this process tree, so the installer survives stopping RunnerDuck.
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(scriptPath);
        Process.Start(startInfo)?.Dispose();
    }
}
