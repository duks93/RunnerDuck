/*
 MetricsBarItems.cs
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

using System.Globalization;

namespace RunnerDuck.Core;

public enum MetricsBarItemKind
{
    CPU,
    Memory,
    Storage,
    Battery,
    Network,
    CustomMetrics,
}

public sealed record MetricsBarItem(
    string Key,
    MetricsBarItemKind Kind,
    string IconText,
    string DetailText,
    bool HasValue = true,
    Guid? CustomMetricsSourceID = null,
    string? Title = null
);

public static class MetricsBarItems
{
    public const int MaximumIconTextLength = 4;

    public static IReadOnlyList<MetricsBarItem> Build(
        MetricsBarConfiguration configuration,
        SystemInfoBundle systemInfoBundle,
        IReadOnlyList<CustomMetricsBundle> customMetricsBundles,
        IFormatProvider? provider = null
    )
    {
        var items = new List<MetricsBarItem>();
        if (configuration.ShowsCPU && systemInfoBundle.CPUInfo is { } cpuInfo)
        {
            items.Add(PercentageItem(MetricsBarItemKind.CPU, cpuInfo.Percentage, provider));
        }
        if (configuration.ShowsMemory && systemInfoBundle.MemoryInfo is { } memoryInfo)
        {
            items.Add(PercentageItem(MetricsBarItemKind.Memory, memoryInfo.Percentage, provider));
        }
        if (configuration.ShowsStorage && systemInfoBundle.StorageInfo is { } storageInfo)
        {
            items.Add(PercentageItem(MetricsBarItemKind.Storage, storageInfo.Percentage, provider));
        }
        if (configuration.ShowsBattery && systemInfoBundle.BatteryInfo is { } batteryInfo)
        {
            items.Add(batteryInfo.IsInstalled
                ? PercentageItem(MetricsBarItemKind.Battery, batteryInfo.Percentage, provider)
                : new MetricsBarItem(nameof(MetricsBarItemKind.Battery), MetricsBarItemKind.Battery, CustomMetricsBundle.MissingValueLabel, "", HasValue: false));
        }
        if (configuration.ShowsNetwork && systemInfoBundle.NetworkInfo is { } networkInfo)
        {
            items.Add(new MetricsBarItem(
                nameof(MetricsBarItemKind.Network),
                MetricsBarItemKind.Network,
                MetricsFormatter.FormatCompactSpeed(networkInfo.Download, provider),
                $"⬆{MetricsFormatter.FormatSpeed(networkInfo.Upload, provider)} ⬇{MetricsFormatter.FormatSpeed(networkInfo.Download, provider)}"
            ));
        }
        items.AddRange(customMetricsBundles
            .Where(bundle => configuration.VisibleCustomMetricsSourceIDs.Contains(bundle.ID))
            .Select(bundle => new MetricsBarItem(
                $"{nameof(MetricsBarItemKind.CustomMetrics)}:{bundle.ID}",
                MetricsBarItemKind.CustomMetrics,
                Truncate(bundle.MetricsBarLabel, MaximumIconTextLength),
                bundle.MetricsBarLabel,
                HasValue: bundle.MetricsBarLabel != CustomMetricsBundle.MissingValueLabel,
                CustomMetricsSourceID: bundle.ID,
                Title: bundle.Snapshot.Title
            )));
        return items;
    }

    private static MetricsBarItem PercentageItem(MetricsBarItemKind kind, Percentage percentage, IFormatProvider? provider) =>
        new(
            kind.ToString(),
            kind,
            MetricsFormatter.FormatCompactPercentage(percentage, provider),
            MetricsFormatter.FormatPercentage(percentage, provider).Trim()
        );

    private static string Truncate(string text, int maximumLength)
    {
        var info = new StringInfo(text);
        return info.LengthInTextElements <= maximumLength ? text : info.SubstringByTextElements(0, maximumLength);
    }
}
