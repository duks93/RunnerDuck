/*
 DashboardRows.cs
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

public enum DashboardDetailKind
{
    System,
    User,
    Idle,
    Used,
    Available,
    Total,
    Charging,
    PluggedIn,
    OnBattery,
    IPAddress,
    Upload,
    Download,
}

public sealed record DashboardDetail(DashboardDetailKind Kind, string Value = "");

public sealed record DashboardSystemRow(
    SystemMetricKind Kind,
    string? Value,
    IReadOnlyList<DashboardDetail> Details,
    IReadOnlyList<double> LineGraphValues,
    double? BarGraphValue = null,
    NetworkInterface? NetworkInterface = null
)
{
    public bool Equals(DashboardSystemRow? other) =>
        other is not null
        && Kind == other.Kind
        && Value == other.Value
        && Details.SequenceEqual(other.Details)
        && LineGraphValues.SequenceEqual(other.LineGraphValues)
        && BarGraphValue == other.BarGraphValue
        && NetworkInterface == other.NetworkInterface;

    public override int GetHashCode() => HashCode.Combine(Kind, Value, Details.Count, LineGraphValues.Count, BarGraphValue, NetworkInterface);
}

public static class DashboardRows
{
    public const double MinimumGraphValue = 2;
    public const double MaximumGraphValue = 100;

    public static IReadOnlyList<DashboardSystemRow> Build(Metrics metrics, IFormatProvider? provider = null)
    {
        var bundle = metrics.SystemInfoBundle;
        var rows = new List<DashboardSystemRow>();
        if (bundle.CPUInfo is { } cpuInfo)
        {
            rows.Add(new DashboardSystemRow(
                SystemMetricKind.CPU,
                Percentage(cpuInfo.Percentage, provider),
                [
                    new(DashboardDetailKind.System, Percentage(cpuInfo.System, provider)),
                    new(DashboardDetailKind.User, Percentage(cpuInfo.User, provider)),
                    new(DashboardDetailKind.Idle, Percentage(cpuInfo.Idle, provider)),
                ],
                GraphValues(metrics.CPURingBuffer)
            ));
        }
        if (bundle.MemoryInfo is { } memoryInfo)
        {
            rows.Add(new DashboardSystemRow(
                SystemMetricKind.Memory,
                Percentage(memoryInfo.Percentage, provider),
                [
                    new(DashboardDetailKind.Used, Bytes(memoryInfo.Used, provider)),
                    new(DashboardDetailKind.Available, Bytes(memoryInfo.Available, provider)),
                    new(DashboardDetailKind.Total, Bytes(memoryInfo.Total, provider)),
                ],
                GraphValues(metrics.MemoryRingBuffer)
            ));
        }
        if (bundle.StorageInfo is { } storageInfo)
        {
            rows.Add(new DashboardSystemRow(
                SystemMetricKind.Storage,
                Percentage(storageInfo.Percentage, provider),
                [
                    new(DashboardDetailKind.Used, Storage(storageInfo.Used, provider)),
                    new(DashboardDetailKind.Available, Storage(storageInfo.Available, provider)),
                    new(DashboardDetailKind.Total, Storage(storageInfo.Total, provider)),
                ],
                [],
                Math.Clamp(storageInfo.Percentage.Value, 0, MaximumGraphValue)
            ));
        }
        if (bundle.BatteryInfo is { } batteryInfo)
        {
            rows.Add(batteryInfo.IsInstalled
                ? new DashboardSystemRow(
                    SystemMetricKind.Battery,
                    Percentage(batteryInfo.Percentage, provider),
                    [new(PowerSource(batteryInfo))],
                    []
                )
                : new DashboardSystemRow(SystemMetricKind.Battery, null, [], []));
        }
        if (bundle.NetworkInfo is { } networkInfo)
        {
            rows.Add(networkInfo.HasConnection
                ? new DashboardSystemRow(
                    SystemMetricKind.Network,
                    null,
                    NetworkDetails(networkInfo, provider),
                    [],
                    NetworkInterface: networkInfo.NetworkInterface
                )
                : new DashboardSystemRow(SystemMetricKind.Network, null, [], []));
        }
        return rows;
    }

    private static IReadOnlyList<DashboardDetail> NetworkDetails(NetworkInfo networkInfo, IFormatProvider? provider)
    {
        var details = new List<DashboardDetail>();
        if (networkInfo.IPAddress is { } ipAddress)
        {
            details.Add(new(DashboardDetailKind.IPAddress, ipAddress));
        }
        details.Add(new(DashboardDetailKind.Upload, MetricsFormatter.FormatSpeed(networkInfo.Upload, provider).Trim()));
        details.Add(new(DashboardDetailKind.Download, MetricsFormatter.FormatSpeed(networkInfo.Download, provider).Trim()));
        return details;
    }

    private static DashboardDetailKind PowerSource(BatteryInfo batteryInfo) => batteryInfo switch
    {
        { IsCharging: true } => DashboardDetailKind.Charging,
        { IsPluggedIn: true } => DashboardDetailKind.PluggedIn,
        _ => DashboardDetailKind.OnBattery,
    };

    private static IReadOnlyList<double> GraphValues(RingBuffer ringBuffer) =>
        ringBuffer.Values.Select(value => Math.Clamp(value, MinimumGraphValue, MaximumGraphValue)).ToList();

    private static string Percentage(Percentage percentage, IFormatProvider? provider) =>
        MetricsFormatter.FormatPercentage(percentage, provider).Trim();

    private static string Bytes(ByteData bytes, IFormatProvider? provider) =>
        MetricsFormatter.FormatBytes(bytes, provider).Trim();

    private static string Storage(ByteData bytes, IFormatProvider? provider) =>
        MetricsFormatter.FormatStorage(bytes, provider).Trim();
}
