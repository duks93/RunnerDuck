namespace RunnerDuck.Core.Tests;

public sealed class RunnerSizeTests
{
    [Theory]
    [InlineData(RunnerSize.Small)]
    [InlineData(RunnerSize.Standard)]
    [InlineData(RunnerSize.Large)]
    public void Size_is_restored_after_reopening_settings(RunnerSize size)
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        new AppSettingsRepository(path).Save(new AppSettings { RunnerSize = size, IsFlippedHorizontally = true });

        var restored = new AppSettingsRepository(path).Load();

        Assert.Equal(size, restored.RunnerSize);
        Assert.True(restored.IsFlippedHorizontally);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("\"large\"")]
    public void Invalid_size_keeps_other_settings_and_uses_standard(string value)
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        File.WriteAllText(path, $$"""{"runnerSize":{{value}},"isFlippedHorizontally":true}""");

        var restored = new AppSettingsRepository(path).Load();

        Assert.Equal(RunnerSize.Standard, restored.RunnerSize);
        Assert.True(restored.IsFlippedHorizontally);
    }

    [Fact]
    public void Existing_settings_keep_the_original_size()
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        File.WriteAllText(path, """{"runnerID":"duck-filled"}""");
        var restored = new AppSettingsRepository(path).Load();
        Assert.Equal(RunnerSize.Standard, restored.RunnerSize);
        Assert.Equal("duck-filled", restored.RunnerID);
    }
}
