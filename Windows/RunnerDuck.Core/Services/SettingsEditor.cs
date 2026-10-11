/*
 SettingsEditor.cs
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

namespace RunnerDuck.Core;

public enum SystemMetricKind
{
    CPU,
    Memory,
    Storage,
    Battery,
    Network,
}

public static class SettingsEditor
{
    public static void SaveCustomMetricsChange(AppSettings settings, Action<AppSettings> change, Action<AppSettings> save)
    {
        var sources = settings.CustomMetricsConfiguration.Sources.ToList();
        var visibleIDs = settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs.ToHashSet();
        try
        {
            change(settings);
            save(settings);
        }
        catch
        {
            settings.CustomMetricsConfiguration.Sources = sources;
            settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs = visibleIDs;
            throw;
        }
    }

    public static void AddCustomMetricsSource(AppSettings settings, CustomMetricsSource source) =>
        settings.CustomMetricsConfiguration.Sources.Add(source);

    public static void RemoveCustomMetricsSource(AppSettings settings, Guid sourceID)
    {
        settings.CustomMetricsConfiguration.Sources.RemoveAll(source => source.ID == sourceID);
        settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs.Remove(sourceID);
    }

    public static void MoveCustomMetricsSource(AppSettings settings, int fromIndex, int toIndex)
    {
        var sources = settings.CustomMetricsConfiguration.Sources;
        if (fromIndex < 0 || fromIndex >= sources.Count || toIndex < 0 || toIndex >= sources.Count || fromIndex == toIndex)
        {
            return;
        }
        var source = sources[fromIndex];
        sources.RemoveAt(fromIndex);
        sources.Insert(toIndex, source);
    }

    public static bool IsMonitored(AppSettings settings, SystemMetricKind kind) => kind switch
    {
        SystemMetricKind.CPU => true,
        SystemMetricKind.Memory => settings.SystemMetricsConfiguration.MonitorsMemory,
        SystemMetricKind.Storage => settings.SystemMetricsConfiguration.MonitorsStorage,
        SystemMetricKind.Battery => settings.SystemMetricsConfiguration.MonitorsBattery,
        SystemMetricKind.Network => settings.SystemMetricsConfiguration.MonitorsNetwork,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static void SetMonitors(AppSettings settings, SystemMetricKind kind, bool isOn)
    {
        var configuration = settings.SystemMetricsConfiguration;
        switch (kind)
        {
            case SystemMetricKind.Memory:
                configuration.MonitorsMemory = isOn;
                break;
            case SystemMetricKind.Storage:
                configuration.MonitorsStorage = isOn;
                break;
            case SystemMetricKind.Battery:
                configuration.MonitorsBattery = isOn;
                break;
            case SystemMetricKind.Network:
                configuration.MonitorsNetwork = isOn;
                break;
        }
    }

    public static void SetMonitoring(AppSettings settings, SystemMetricKind kind, bool isOn)
    {
        if (kind == SystemMetricKind.CPU)
        {
            return;
        }
        SetMonitors(settings, kind, isOn);
        if (!isOn)
        {
            SetShowsInMetricsBar(settings, kind, false);
        }
    }

    public static bool ShowsInMetricsBar(AppSettings settings, SystemMetricKind kind) => kind switch
    {
        SystemMetricKind.CPU => settings.MetricsBarConfiguration.ShowsCPU,
        SystemMetricKind.Memory => settings.MetricsBarConfiguration.ShowsMemory,
        SystemMetricKind.Storage => settings.MetricsBarConfiguration.ShowsStorage,
        SystemMetricKind.Battery => settings.MetricsBarConfiguration.ShowsBattery,
        SystemMetricKind.Network => settings.MetricsBarConfiguration.ShowsNetwork,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static bool SetShowsInMetricsBar(AppSettings settings, SystemMetricKind kind, bool isOn)
    {
        var configuration = settings.MetricsBarConfiguration;
        switch (kind)
        {
            case SystemMetricKind.CPU:
                configuration.ShowsCPU = isOn;
                break;
            case SystemMetricKind.Memory:
                configuration.ShowsMemory = isOn;
                break;
            case SystemMetricKind.Storage:
                configuration.ShowsStorage = isOn;
                break;
            case SystemMetricKind.Battery:
                configuration.ShowsBattery = isOn;
                break;
            case SystemMetricKind.Network:
                configuration.ShowsNetwork = isOn;
                break;
        }
        if (!isOn || IsMonitored(settings, kind))
        {
            return false;
        }
        SetMonitors(settings, kind, true);
        return true;
    }

    public static void SetShowsCustomMetricsInMetricsBar(AppSettings settings, Guid sourceID, bool isOn)
    {
        var visibleIDs = settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs;
        if (isOn)
        {
            visibleIDs.Add(sourceID);
        }
        else
        {
            visibleIDs.Remove(sourceID);
        }
    }
}
