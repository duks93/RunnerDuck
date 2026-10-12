using Microsoft.Win32;
using RunnerDuck.App.Settings;
using RunnerDuck.App.Tray;

namespace RunnerDuck.App;

public sealed class TrayLaunchGuardTests : IDisposable
{
    private const string Installed = @"C:\Users\Test User\AppData\Local\RunnerDuck\TrayApp\RunnerDuck.exe";
    private const string Packaged = @"C:\Users\Test User\AppData\Local\RunnerDuck\App\RunnerDuck.exe";
    private readonly string testKey = @"Software\RunnerDuck.Tests\LaunchGuard-" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey root;

    public TrayLaunchGuardTests() => root = Registry.CurrentUser.CreateSubKey(testKey);

    [Theory]
    [InlineData(Installed, Installed, false, true)]
    [InlineData(@"c:\users\test user\appdata\local\runnerduck\trayapp\RUNNERDUCK.EXE", Installed, false, true)]
    [InlineData(@"C:\repo\bin\x64\Release\net10.0-windows\RunnerDuck.exe", Installed, false, false)]
    [InlineData(@"C:\repo\bin\x64\Release\net10.0-windows\win-x64\RunnerDuck.exe", Installed, false, false)]
    [InlineData(@"C:\Copied installation\RunnerDuck.exe", Installed, false, false)]
    [InlineData(Installed, null, false, true)]
    [InlineData(@"C:\Downloads\RunnerDuck.exe", null, false, false)]
    [InlineData(Installed, Packaged, false, false)]
    [InlineData(Packaged, Packaged, false, true)]
    [InlineData(@"C:\legacy\bin\RunnerDuck.exe", Installed, true, true)]
    [InlineData(@"\\?\C:\Users\Test User\AppData\Local\RunnerDuck\TrayApp\RunnerDuck.exe", Installed, false, false)]
    [InlineData(@"C:\Users\TESTUS~1\AppData\Local\RunnerDuck\TrayApp\RunnerDuck.exe", Installed, false, false)]
    [InlineData(null, Installed, false, false)]
    public void Only_the_registered_or_legacy_default_location_can_start(
        string? executable, string? registration, bool packaged, bool allowed)
    {
        Assert.Equal(allowed, TrayLaunchGuard.IsAllowed(executable, registration, packaged, Installed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("RunnerDuck.exe")]
    [InlineData(@"C:RunnerDuck.exe")]
    [InlineData(@"C:\other\App.exe")]
    [InlineData(@"C:\other\RunnerDuck.exe --argument")]
    [InlineData(@"C:\other\RunnerDuck.exe:stream")]
    [InlineData(@"C:\invalid?\RunnerDuck.exe")]
    public void Malformed_registration_falls_back_only_to_the_legacy_default(string registration)
    {
        WriteRegistration(registration);
        Assert.Null(TrayLaunchGuard.ReadInstalledExecutable(root));
        Assert.True(TrayLaunchGuard.IsAllowed(Installed, registration, false, Installed));
        Assert.False(TrayLaunchGuard.IsAllowed(@"C:\bin\RunnerDuck.exe", registration, false, Installed));
    }

    [Theory]
    [InlineData(RegistryValueKind.ExpandString)]
    [InlineData(RegistryValueKind.MultiString)]
    [InlineData(RegistryValueKind.DWord)]
    public void Registration_requires_a_plain_string(RegistryValueKind kind)
    {
        using var key = root.CreateSubKey(TrayLaunchGuard.RegistrationKey);
        object value = kind switch
        {
            RegistryValueKind.MultiString => new[] { Installed },
            RegistryValueKind.DWord => 1,
            _ => Installed,
        };
        key.SetValue(TrayLaunchGuard.RegistrationValue, value, kind);
        Assert.Null(TrayLaunchGuard.ReadInstalledExecutable(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_launch_never_runs_startup_or_tray_initialization(bool existingPreferences)
    {
        WriteRegistration(Installed);
        var startup = new TrayLaunchAtLogin(root);
        if (existingPreferences) startup.Set(false, Installed);
        var started = false;
        Assert.False(TrayLaunchGuard.RunIfAllowed(@"C:\Copied installation\RunnerDuck.exe",
            TrayLaunchGuard.ReadInstalledExecutable(root), false, Installed, () =>
            {
                started = true;
                startup.EnableByDefault(@"C:\Copied installation\RunnerDuck.exe");
            }));
        Assert.False(started);
        using var preferences = root.OpenSubKey(TrayLaunchGuard.RegistrationKey);
        Assert.Equal(existingPreferences ? 1 : null, preferences!.GetValue("LaunchAtLoginInitialized"));
        Assert.Equal(Installed, preferences.GetValue(TrayLaunchGuard.RegistrationValue));
        Assert.False(startup.IsEnabled);
    }

    [Fact]
    public void Allowed_launch_invokes_the_start_action_once()
    {
        WriteRegistration(Installed);
        var starts = 0;
        Assert.True(TrayLaunchGuard.RunIfAllowed(Installed, TrayLaunchGuard.ReadInstalledExecutable(root),
            false, Installed, () => starts++));
        Assert.Equal(1, starts);
    }

    private void WriteRegistration(string path)
    {
        using var key = root.CreateSubKey(TrayLaunchGuard.RegistrationKey);
        key.SetValue(TrayLaunchGuard.RegistrationValue, path, RegistryValueKind.String);
    }

    public void Dispose()
    {
        root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(testKey);
    }
}
