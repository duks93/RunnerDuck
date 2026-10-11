/*
 SystemInfo.cs
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

public sealed record CPUInfo(
    Percentage Percentage = default,
    Percentage System = default,
    Percentage User = default,
    Percentage Idle = default
);

public sealed record MemoryInfo(
    Percentage Percentage = default,
    ByteData Used = default,
    ByteData Available = default,
    ByteData Total = default
);

public sealed record StorageInfo(
    Percentage Percentage = default,
    ByteData Total = default,
    ByteData Available = default,
    ByteData Used = default
);

public sealed record BatteryInfo(
    Percentage Percentage = default,
    bool IsInstalled = false,
    bool IsCharging = false,
    bool IsPluggedIn = false
);

public enum NetworkInterface
{
    Unknown,
    WiFi,
    Cellular,
    Ethernet,
    Loopback,
}

public sealed record NetworkInfo(
    bool HasConnection = false,
    NetworkInterface NetworkInterface = NetworkInterface.Unknown,
    string? IPAddress = null,
    ByteData Upload = default,
    ByteData Download = default
);

public sealed record SystemInfoBundle(
    CPUInfo? CPUInfo = null,
    MemoryInfo? MemoryInfo = null,
    StorageInfo? StorageInfo = null,
    BatteryInfo? BatteryInfo = null,
    NetworkInfo? NetworkInfo = null
)
{
    public static SystemInfoBundle CPUZero() => new(CPUInfo: new());
}
