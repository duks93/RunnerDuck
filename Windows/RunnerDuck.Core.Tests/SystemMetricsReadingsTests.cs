namespace RunnerDuck.Core.Tests;

public sealed class SystemMetricsReadingsTests
{
    private static NetworkAdapter Adapter(
        string id,
        string description = "Intel Wi-Fi 7",
        NetworkInterface kind = NetworkInterface.WiFi,
        bool isUp = true,
        bool isLoopbackOrTunnel = false,
        bool hasGateway = true,
        string? ipv4 = "192.168.0.10"
    ) => new(id, description, kind, isUp, isLoopbackOrTunnel, hasGateway, 100, 200, ipv4);

    [Fact]
    public void SelectNetwork_prefers_usable_adapter_with_gateway()
    {
        var counters = SystemMetricsReadings.SelectNetwork([
            Adapter("down", isUp: false),
            Adapter("loopback", kind: NetworkInterface.Loopback, isLoopbackOrTunnel: true),
            Adapter("no-gateway", description: "Realtek Ethernet", kind: NetworkInterface.Ethernet, hasGateway: false),
            Adapter("wifi"),
        ]);
        Assert.Equal(new NetworkCounters("wifi", 100, 200, NetworkInterface.WiFi, "192.168.0.10"), counters);
    }

    [Theory]
    [InlineData("WireGuard VPN Tunnel")]
    [InlineData("Hyper-V Virtual Ethernet Adapter")]
    [InlineData("TAP-Windows Adapter V9")]
    [InlineData("vEthernet (WSL)")]
    public void SelectNetwork_skips_virtual_adapters(string description)
    {
        var counters = SystemMetricsReadings.SelectNetwork([
            Adapter("virtual", description: description, kind: NetworkInterface.Ethernet),
            Adapter("wifi", hasGateway: false),
        ]);
        Assert.Equal("wifi", counters?.InterfaceID);
    }

    [Fact]
    public void SelectNetwork_returns_null_when_only_virtual_or_down_adapters_exist()
    {
        Assert.Null(SystemMetricsReadings.SelectNetwork([
            Adapter("vpn", description: "Corporate VPN"),
            Adapter("down", isUp: false),
        ]));
    }

    [Fact]
    public void SelectNetwork_keeps_ipv6_only_adapter_without_ipv4_address()
    {
        Assert.Equal(new NetworkCounters("ipv6", 100, 200, NetworkInterface.WiFi, null),
            SystemMetricsReadings.SelectNetwork([Adapter("ipv6", ipv4: null)]));
    }

    [Fact]
    public void Memory_reports_used_available_and_total()
    {
        Assert.Equal(
            new MemoryInfo(new Percentage(0.25), new ByteData(4_000), new ByteData(12_000), new ByteData(16_000)),
            SystemMetricsReadings.Memory(16_000, 12_000)
        );
    }

    [Fact]
    public void Memory_handles_missing_total_and_available_above_total()
    {
        Assert.Null(SystemMetricsReadings.Memory(0, 0));
        Assert.Equal(new Percentage(0), SystemMetricsReadings.Memory(1_000, 2_000)!.Percentage);
    }

    [Fact]
    public void Storage_reports_used_from_available_space()
    {
        Assert.Equal(
            new StorageInfo(new Percentage(0.75), new ByteData(1_000), new ByteData(250), new ByteData(750)),
            SystemMetricsReadings.Storage(1_000, 250)
        );
        Assert.Null(SystemMetricsReadings.Storage(0, 0));
    }

    [Theory]
    [InlineData(false, true, 0.5)]
    [InlineData(true, false, 0.5)]
    [InlineData(true, true, 2.55)]
    [InlineData(true, true, -0.1)]
    public void Battery_without_reliable_reading_is_not_installed(bool hasBattery, bool isStatusKnown, double lifeRatio)
    {
        Assert.Equal(new BatteryInfo(), SystemMetricsReadings.Battery(new BatteryReading(hasBattery, isStatusKnown, lifeRatio, false, false)));
    }

    [Fact]
    public void Battery_reports_charge_and_power_source()
    {
        Assert.Equal(
            new BatteryInfo(new Percentage(0.62), IsInstalled: true, IsCharging: true, IsPluggedIn: true),
            SystemMetricsReadings.Battery(new BatteryReading(true, true, 0.62, true, true))
        );
    }
}
