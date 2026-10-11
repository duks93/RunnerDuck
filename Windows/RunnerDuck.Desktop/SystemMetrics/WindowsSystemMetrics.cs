/*
 WindowsSystemMetrics.cs
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

// Adapted from RunCat365 (Apache License 2.0): https://github.com/runcat-dev/RunCat365

using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using RunnerDuck.Core;
using CoreNetworkInterface = RunnerDuck.Core.NetworkInterface;
using SystemNetworkInterface = System.Net.NetworkInformation.NetworkInterface;

namespace RunnerDuck.App.SystemMetrics;

internal static partial class WindowsSystemMetrics
{
    public static SystemMetricsEnvironment Environment(CPUMonitor cpuMonitor) => new()
    {
        SampleCPU = cpuMonitor.Sample,
        SampleMemory = SampleMemory,
        SampleStorage = SampleStorage,
        SampleBattery = SampleBattery,
        SampleNetwork = SampleNetwork,
        Now = () => DateTimeOffset.Now,
    };

    private static MemoryInfo? SampleMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? SystemMetricsReadings.Memory(status.TotalPhysical, status.AvailablePhysical)
            : null;
    }

    private static StorageInfo? SampleStorage()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(System.Environment.SystemDirectory) ?? @"C:\");
            return drive.IsReady ? SystemMetricsReadings.Storage(drive.TotalSize, drive.AvailableFreeSpace) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static BatteryInfo? SampleBattery()
    {
        var status = SystemInformation.PowerStatus;
        return SystemMetricsReadings.Battery(new BatteryReading(
            HasBattery: !status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery),
            IsStatusKnown: status.BatteryChargeStatus != BatteryChargeStatus.Unknown,
            LifeRatio: status.BatteryLifePercent,
            IsCharging: status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging),
            IsPluggedIn: status.PowerLineStatus == PowerLineStatus.Online
        ));
    }

    private static NetworkCounters? SampleNetwork()
    {
        try
        {
            return SystemMetricsReadings.SelectNetwork(SystemNetworkInterface.GetAllNetworkInterfaces().Select(Adapter));
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static NetworkAdapter Adapter(SystemNetworkInterface networkInterface)
    {
        IPInterfaceProperties properties;
        IPInterfaceStatistics statistics;
        try
        {
            properties = networkInterface.GetIPProperties();
            statistics = networkInterface.GetIPStatistics();
        }
        catch (NetworkInformationException)
        {
            return new NetworkAdapter(networkInterface.Id, networkInterface.Description, CoreNetworkInterface.Unknown, false, false, false, 0, 0, null);
        }
        return new NetworkAdapter(
            networkInterface.Id,
            networkInterface.Description,
            Kind(networkInterface.NetworkInterfaceType),
            networkInterface.OperationalStatus == OperationalStatus.Up,
            networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel,
            properties.GatewayAddresses.Count > 0,
            statistics.BytesSent,
            statistics.BytesReceived,
            properties.UnicastAddresses
                .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString()
        );
    }

    private static CoreNetworkInterface Kind(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => CoreNetworkInterface.WiFi,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => CoreNetworkInterface.Ethernet,
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 or NetworkInterfaceType.Ppp => CoreNetworkInterface.Cellular,
        NetworkInterfaceType.Loopback => CoreNetworkInterface.Loopback,
        _ => CoreNetworkInterface.Unknown,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
