/*
 AppPaths.cs
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

using RunnerDuck.AIUsage;
using RunnerDuck.Core;
using Windows.Storage;

namespace RunnerDuck.App;

internal static class AppPaths
{
    private static readonly Lazy<string> dataDirectory = new(ResolveDataDirectory);

    public static string DataDirectory => dataDirectory.Value;
    public static string SettingsFile => Path.Combine(DataDirectory, AppSettingsRepository.FileName);
    public static string CustomRunnersFile => Path.Combine(DataDirectory, CustomRunnerRepository.FileName);
    public static string CustomRunnersDirectory => Path.Combine(DataDirectory, "CustomRunners");
    public static string AIUsageSnapshotFile => Path.Combine(DataDirectory, AIUsageSnapshotRepository.FileName);

    private static string ResolveDataDirectory()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch (Exception)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RunnerDuck");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
