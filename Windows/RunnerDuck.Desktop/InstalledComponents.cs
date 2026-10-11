namespace RunnerDuck.App;

internal sealed record InstalledComponents(bool Widget, bool Tray)
{
    public static InstalledComponents Parse(string mode) => mode.Trim().ToLowerInvariant() switch
    {
        "widget" => new(true, false),
        "tray" => new(false, true),
        "both" => new(true, true),
        _ => throw new InvalidDataException("Invalid RunnerDuck installation mode.")
    };

    public static InstalledComponents Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "install-mode.txt");
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Parse("Both");
    }
}
