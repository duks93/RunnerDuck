/*
 Metrics.cs
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

public sealed class Metrics
{
    public SystemInfoBundle SystemInfoBundle { get; private set; } = SystemInfoBundle.CPUZero();
    public RingBuffer CPURingBuffer { get; } = new();
    public RingBuffer MemoryRingBuffer { get; } = new();

    public void ApplyConfiguration(SystemMetricsConfiguration configuration)
    {
        SystemInfoBundle = SystemInfoBundle with
        {
            MemoryInfo = configuration.MonitorsMemory ? SystemInfoBundle.MemoryInfo : null,
            StorageInfo = configuration.MonitorsStorage ? SystemInfoBundle.StorageInfo : null,
            BatteryInfo = configuration.MonitorsBattery ? SystemInfoBundle.BatteryInfo : null,
            NetworkInfo = configuration.MonitorsNetwork ? SystemInfoBundle.NetworkInfo : null,
        };
    }

    public void Update(SystemInfoBundle systemInfoBundle)
    {
        SystemInfoBundle = systemInfoBundle;
        if (systemInfoBundle.CPUInfo is { } cpuInfo)
        {
            CPURingBuffer.Append(cpuInfo.Percentage.Value);
        }
        if (systemInfoBundle.MemoryInfo is { } memoryInfo)
        {
            MemoryRingBuffer.Append(memoryInfo.Percentage.Value);
        }
    }
}
