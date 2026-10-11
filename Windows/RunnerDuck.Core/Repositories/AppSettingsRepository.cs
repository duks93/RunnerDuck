/*
 AppSettingsRepository.cs
 RunnerDuck.Core

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

using System.Text.Json;
using System.Text.Json.Nodes;
using RunnerDuck.AIUsage;

namespace RunnerDuck.Core;

public sealed class AppSettingsRepository(string filePath)
{
    public const string FileName = "settings.json";

    private readonly JSONFileStore<AppSettings> store = new(filePath);

    public AppSettings Load()
    {
        if (ReadRoot() is not { } root)
        {
            return new();
        }
        var defaults = new AppSettings();
        return new()
        {
            RunnerID = Read(root, nameof(AppSettings.RunnerID), defaults.RunnerID),
            SpeedDecreasesUnderLoad = Read(root, nameof(AppSettings.SpeedDecreasesUnderLoad), defaults.SpeedDecreasesUnderLoad),
            IsFlippedHorizontally = Read(root, nameof(AppSettings.IsFlippedHorizontally), defaults.IsFlippedHorizontally),
            RunnerSize = (RunnerSize)Read(root, nameof(AppSettings.RunnerSize), (int)defaults.RunnerSize),
            UpdateInterval = (UpdateInterval)Read(root, nameof(AppSettings.UpdateInterval), (int)defaults.UpdateInterval),
            SystemMetricsConfiguration = Read(root, nameof(AppSettings.SystemMetricsConfiguration), defaults.SystemMetricsConfiguration),
            ShowsMetricsBar = Read(root, nameof(AppSettings.ShowsMetricsBar), defaults.ShowsMetricsBar),
            MetricsBarConfiguration = Read(root, nameof(AppSettings.MetricsBarConfiguration), defaults.MetricsBarConfiguration),
            CustomMetricsConfiguration = ValidOrDefault(
                Read(root, nameof(AppSettings.CustomMetricsConfiguration), defaults.CustomMetricsConfiguration),
                configuration => configuration.Sources.All(source => source is not null),
                defaults.CustomMetricsConfiguration
            ),
            AIUsageConfiguration = Read(root, nameof(AppSettings.AIUsageConfiguration), defaults.AIUsageConfiguration),
        };
    }

    public void Save(AppSettings settings) => store.Save(settings);

    public AIUsageConfiguration? LoadAIUsageConfiguration()
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (JsonNode.Parse(stream) is not JsonObject root) return null;
            return root.TryGetPropertyValue("aiUsageConfiguration", out var node)
                ? node?.Deserialize<AIUsageConfiguration>(JSONFileStore<AppSettings>.Options)
                : new AIUsageConfiguration();
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return new(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static T Read<T>(JsonObject root, string propertyName, T fallback)
    {
        var name = JSONFileStore<AppSettings>.Options.PropertyNamingPolicy!.ConvertName(propertyName);
        if (!root.TryGetPropertyValue(name, out var node) || node is null)
        {
            return fallback;
        }
        try
        {
            return node.Deserialize<T>(JSONFileStore<AppSettings>.Options) ?? fallback;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return fallback;
        }
    }

    private static T ValidOrDefault<T>(T value, Func<T, bool> isValid, T fallback) => isValid(value) ? value : fallback;

    private JsonObject? ReadRoot()
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
