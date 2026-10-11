using RunnerDuck.Core;

namespace RunnerDuck.Core.Tests;

public sealed class AppUpdateInstallationTests
{
    private const string Commit = "25568fdb1e940c160ea8850084df218c1f4c81cb";

    [Fact]
    public void DownloadsSourceArchiveOfPublishedCommit() =>
        Assert.Equal("https://github.com/duks93/RunnerDuck/archive/" + Commit + ".zip", AppUpdateInstallation.SourceArchiveURL(Commit).AbsoluteUri);

    [Theory]
    [InlineData(true, true, "Both")]
    [InlineData(true, false, "Widget")]
    [InlineData(false, true, "Tray")]
    public void KeepsInstalledComponents(bool widget, bool tray, string expected) =>
        Assert.Equal(expected, AppUpdateInstallation.InstallMode(widget, tray));

    [Fact]
    public void ScriptRunsInstallerWithCommitAndRemovesSourceOnlyAfterSuccess()
    {
        var source = AppUpdateInstallation.SourceDirectory(@"C:\Users\me\AppData\Local\RunnerDuck\Update", Commit);
        var lines = AppUpdateInstallation.Script(source, "Tray", Commit).Split("\r\n");
        Assert.Equal(
            [
                "@echo off",
                "chcp 65001 > nul",
                "title RunnerDuck update",
                "set RUNNERDUCK_SOURCE_COMMIT=" + Commit,
                "\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoProfile -ExecutionPolicy Bypass -File \"C:\\Users\\me\\AppData\\Local\\RunnerDuck\\Update\\RunnerDuck-" + Commit + "\\Windows\\Install-RunnerDuck.ps1\" -InstallMode Tray",
                "if errorlevel 1 (",
                "    echo.",
                "    echo RunnerDuck update failed. Review the messages above.",
                "    pause",
                "    exit /b 1",
                ")",
                "rmdir /s /q \"C:\\Users\\me\\AppData\\Local\\RunnerDuck\\Update\\RunnerDuck-" + Commit + "\"",
                "",
            ],
            lines);
    }

    [Fact]
    public void ScriptEscapesPercentSignsInPaths()
    {
        var script = AppUpdateInstallation.Script(@"C:\Users\50%off\Update\RunnerDuck-" + Commit, "Both", Commit);
        Assert.Contains(@"C:\Users\50%%off\Update", script);
        Assert.DoesNotContain(@"\50%off\", script);
    }
}
