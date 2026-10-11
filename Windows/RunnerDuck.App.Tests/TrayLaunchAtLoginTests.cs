using Microsoft.Win32;
using RunnerDuck.App.Settings;

namespace RunnerDuck.App;

public sealed class TrayLaunchAtLoginTests : IDisposable
{
    private const string Executable = @"C:\Users\Test User\RunnerDuck\RunnerDuck.exe";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string testKey = @"Software\RunnerDuck.Tests\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey root;

    public TrayLaunchAtLoginTests() => root = Registry.CurrentUser.CreateSubKey(testKey);

    [Fact]
    public void First_launch_registers_the_quoted_executable()
    {
        var startup = new TrayLaunchAtLogin(root);
        startup.EnableByDefault(Executable);
        using var run = root.OpenSubKey(RunKey);
        Assert.Equal($"\"{Executable}\"", run!.GetValue("RunnerDuck"));
        Assert.True(startup.IsEnabled);
    }

    [Fact]
    public void Manual_disable_survives_a_new_app_instance()
    {
        var startup = new TrayLaunchAtLogin(root);
        startup.EnableByDefault(Executable);
        startup.Set(false, Executable);
        var restarted = new TrayLaunchAtLogin(root);
        restarted.EnableByDefault(Executable);
        Assert.False(restarted.IsEnabled);
    }

    [Fact]
    public void Explicit_disable_before_initialization_is_preserved()
    {
        new TrayLaunchAtLogin(root).Set(false, Executable);
        var restarted = new TrayLaunchAtLogin(root);
        restarted.EnableByDefault(Executable);
        Assert.False(restarted.IsEnabled);
    }

    [Fact]
    public void Removed_startup_entry_is_not_recreated_on_next_launch()
    {
        new TrayLaunchAtLogin(root).EnableByDefault(Executable);
        using (var run = root.OpenSubKey(RunKey, writable: true)) run!.DeleteValue("RunnerDuck");
        var restarted = new TrayLaunchAtLogin(root);
        restarted.EnableByDefault(Executable);
        Assert.False(restarted.IsEnabled);
    }

    [Fact]
    public void Existing_startup_command_is_preserved()
    {
        const string previousCommand = "\"C:\\Existing\\RunnerDuck.exe\"";
        using (var run = root.CreateSubKey(RunKey)) run.SetValue("RunnerDuck", previousCommand);
        new TrayLaunchAtLogin(root).EnableByDefault(Executable);
        using var actual = root.OpenSubKey(RunKey);
        Assert.Equal(previousCommand, actual!.GetValue("RunnerDuck"));
    }

    [Fact]
    public void Manual_reenable_works_after_opt_out()
    {
        var startup = new TrayLaunchAtLogin(root);
        startup.Set(false, Executable);
        startup.EnableByDefault(Executable);
        startup.Set(true, Executable);
        Assert.True(startup.IsEnabled);
    }

    public void Dispose()
    {
        root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(testKey);
    }
}
