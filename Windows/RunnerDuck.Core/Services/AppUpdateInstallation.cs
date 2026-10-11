/*
 AppUpdateInstallation.cs
 RunnerDuck.Core

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

namespace RunnerDuck.Core;

public static class AppUpdateInstallation
{
    public const string ScriptFileName = "update.cmd";

    public static Uri SourceArchiveURL(string commit) => new($"https://github.com/duks93/RunnerDuck/archive/{commit}.zip");

    public static string SourceDirectory(string updateDirectory, string commit) => Path.Combine(updateDirectory, $"RunnerDuck-{commit}");

    public static string InstallMode(bool widget, bool tray) => (widget, tray) switch
    {
        (true, true) => "Both",
        (true, false) => "Widget",
        _ => "Tray",
    };

    public static string Script(string sourceDirectory, string installMode, string commit)
    {
        var directory = sourceDirectory.Replace("%", "%%");
        var installer = Path.Combine(directory, "Windows", "Install-RunnerDuck.ps1");
        return string.Join("\r\n",
            "@echo off",
            "chcp 65001 > nul",
            "title RunnerDuck update",
            $"set RUNNERDUCK_SOURCE_COMMIT={commit}",
            $"\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoProfile -ExecutionPolicy Bypass -File \"{installer}\" -InstallMode {installMode}",
            "if errorlevel 1 (",
            "    echo.",
            "    echo RunnerDuck update failed. Review the messages above.",
            "    pause",
            "    exit /b 1",
            ")",
            $"rmdir /s /q \"{directory}\"",
            ""
        );
    }
}
