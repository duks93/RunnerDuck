namespace RunnerDuck.App;

public sealed class InstalledComponentsTests
{
    [Theory]
    [InlineData("Both", true, true)]
    [InlineData("Widget", true, false)]
    [InlineData("Tray", false, true)]
    [InlineData("widget\r\n", true, false)]
    [InlineData("TRAY", false, true)]
    public void InstallerModesSelectOnlyRequestedComponents(string mode, bool widget, bool tray)
    {
        var components = InstalledComponents.Parse(mode);
        Assert.Equal(widget, components.Widget);
        Assert.Equal(tray, components.Tray);
    }

    [Fact]
    public void InvalidModeDoesNotSilentlyEnableComponents()
    {
        Assert.Throws<InvalidDataException>(() => InstalledComponents.Parse("invalid"));
    }
}
