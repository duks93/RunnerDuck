/*
 SystemMetricsSampler.cs
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

public sealed record NetworkCounters(
    string InterfaceID,
    long BytesSent,
    long BytesReceived,
    NetworkInterface NetworkInterface,
    string? IPAddress
);

public sealed class SystemMetricsEnvironment
{
    public required Func<CPUInfo?> SampleCPU { get; init; }
    public required Func<MemoryInfo?> SampleMemory { get; init; }
    public required Func<StorageInfo?> SampleStorage { get; init; }
    public required Func<BatteryInfo?> SampleBattery { get; init; }
    public required Func<NetworkCounters?> SampleNetwork { get; init; }
    public required Func<DateTimeOffset> Now { get; init; }
}

public sealed class SystemMetricsSampler(SystemMetricsEnvironment environment)
{
    private readonly Lock gate = new();
    private int generation;
    private (NetworkCounters Counters, DateTimeOffset Date)? previousNetwork;

    public void Reset()
    {
        lock (gate)
        {
            generation++;
            previousNetwork = null;
        }
    }

    public SystemInfoBundle Sample(SystemMetricsConfiguration configuration)
    {
        int sampleGeneration;
        lock (gate)
        {
            if (!configuration.MonitorsNetwork)
            {
                generation++;
                previousNetwork = null;
            }
            sampleGeneration = generation;
        }
        return new SystemInfoBundle(
            CPUInfo: environment.SampleCPU(),
            MemoryInfo: configuration.MonitorsMemory ? environment.SampleMemory() : null,
            StorageInfo: configuration.MonitorsStorage ? environment.SampleStorage() : null,
            BatteryInfo: configuration.MonitorsBattery ? environment.SampleBattery() ?? new BatteryInfo() : null,
            NetworkInfo: configuration.MonitorsNetwork ? SampleNetwork(sampleGeneration) : null
        );
    }

    private NetworkInfo SampleNetwork(int sampleGeneration)
    {
        var now = environment.Now();
        var counters = environment.SampleNetwork();
        lock (gate)
        {
            if (sampleGeneration != generation)
            {
                return new NetworkInfo();
            }
            if (counters is null)
            {
                previousNetwork = null;
                return new NetworkInfo();
            }
            var previous = previousNetwork;
            previousNetwork = (counters, now);
            if (previous is not { } last || last.Counters.InterfaceID != counters.InterfaceID)
            {
                return NetworkInfoFrom(counters, ByteData.Zero, ByteData.Zero);
            }
            return NetworkInfoFrom(
                counters,
                NetworkSpeed.BytesPerSecond(last.Counters.BytesSent, counters.BytesSent, now - last.Date),
                NetworkSpeed.BytesPerSecond(last.Counters.BytesReceived, counters.BytesReceived, now - last.Date)
            );
        }
    }

    private static NetworkInfo NetworkInfoFrom(NetworkCounters counters, ByteData upload, ByteData download) =>
        new(true, counters.NetworkInterface, counters.IPAddress, upload, download);
}

public static class NetworkSpeed
{
    public static ByteData BytesPerSecond(long previousBytes, long currentBytes, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || currentBytes < previousBytes)
        {
            return ByteData.Zero;
        }
        return new ByteData((currentBytes - previousBytes) / elapsed.TotalSeconds);
    }
}
