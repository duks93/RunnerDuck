/*
 SystemMetricsReadings.cs
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

public sealed record NetworkAdapter(
    string ID,
    string Description,
    NetworkInterface Kind,
    bool IsUp,
    bool IsLoopbackOrTunnel,
    bool HasGateway,
    long BytesSent,
    long BytesReceived,
    string? IPv4Address
);

public sealed record BatteryReading(
    bool HasBattery,
    bool IsStatusKnown,
    double LifeRatio,
    bool IsCharging,
    bool IsPluggedIn
);

public static class SystemMetricsReadings
{
    private static readonly string[] VirtualAdapterKeywords = ["vpn", "virtual", "tap", "tun", "hyper-v", "vethernet"];

    public static NetworkCounters? SelectNetwork(IEnumerable<NetworkAdapter> adapters) =>
        adapters
            .Where(IsUsable)
            .OrderByDescending(adapter => adapter.HasGateway)
            .Select(adapter => new NetworkCounters(
                adapter.ID,
                adapter.BytesSent,
                adapter.BytesReceived,
                adapter.Kind,
                adapter.IPv4Address
            ))
            .FirstOrDefault();

    public static MemoryInfo? Memory(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0)
        {
            return null;
        }
        var available = Math.Min(availableBytes, totalBytes);
        var used = totalBytes - available;
        return new MemoryInfo(
            new Percentage((double)used / totalBytes),
            new ByteData(used),
            new ByteData(available),
            new ByteData(totalBytes)
        );
    }

    public static StorageInfo? Storage(long totalBytes, long availableBytes)
    {
        if (totalBytes <= 0)
        {
            return null;
        }
        var available = Math.Clamp(availableBytes, 0, totalBytes);
        var used = totalBytes - available;
        return new StorageInfo(
            new Percentage((double)used / totalBytes),
            new ByteData(totalBytes),
            new ByteData(available),
            new ByteData(used)
        );
    }

    public static BatteryInfo Battery(BatteryReading reading)
    {
        if (!reading.HasBattery || !reading.IsStatusKnown || reading.LifeRatio is < 0 or > 1)
        {
            return new BatteryInfo();
        }
        return new BatteryInfo(new Percentage(reading.LifeRatio), true, reading.IsCharging, reading.IsPluggedIn);
    }

    private static bool IsUsable(NetworkAdapter adapter)
    {
        if (!adapter.IsUp || adapter.IsLoopbackOrTunnel)
        {
            return false;
        }
        var description = adapter.Description.ToLowerInvariant();
        return !VirtualAdapterKeywords.Any(description.Contains);
    }
}
