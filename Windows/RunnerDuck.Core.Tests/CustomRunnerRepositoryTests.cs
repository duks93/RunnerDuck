namespace RunnerDuck.Core.Tests;

public sealed class CustomRunnerRepositoryTests
{
    [Fact]
    public void Load_returns_empty_list_when_file_is_missing()
    {
        using var directory = new TestDirectory();
        Assert.Empty(new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName)).Load());
    }

    [Fact]
    public void Save_and_load_round_trip_runners_in_order()
    {
        using var directory = new TestDirectory();
        var sut = new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName));
        var first = new Runner("first-id", "First", isTemplate: true, FrameOrder.Ascending(2));
        var second = new Runner("second-id", "Second", isTemplate: false, FrameOrder.Custom([1, 0, 2]));
        sut.Save([first, second]);
        var loaded = new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName)).Load();
        Assert.Equal(new[] { "first-id", "second-id" }, loaded.Select(runner => runner.ID));
        Assert.Equal(new RunnerSource.Custom("First"), loaded[0].Source);
        Assert.True(loaded[0].IsTemplate);
        Assert.Equal(new[] { 0, 1 }, loaded[0].FrameOrder.Order);
        Assert.Equal(new RunnerSource.Custom("Second"), loaded[1].Source);
        Assert.False(loaded[1].IsTemplate);
        Assert.Equal(new[] { 1, 0, 2 }, loaded[1].FrameOrder.Order);
        Assert.Equal("second-id", sut.Load("second-id")?.ID);
        Assert.Null(sut.Load("missing-id"));
    }

    [Fact]
    public void Save_replaces_previous_list_and_keeps_duplicate_frames()
    {
        using var directory = new TestDirectory();
        var filePath = directory.File(CustomRunnerRepository.FileName);
        var sut = new CustomRunnerRepository(filePath);
        sut.Save([new Runner("old-id", "Old", isTemplate: true, FrameOrder.Ascending(2))]);
        sut.Save([new Runner("new-id", "New", isTemplate: true, FrameOrder.Custom([0, 0, 1]))]);
        var loaded = Assert.Single(new CustomRunnerRepository(filePath).Load());
        Assert.Equal("new-id", loaded.ID);
        Assert.Equal(new[] { 0, 0, 1 }, loaded.FrameOrder.Order);
    }

    [Fact]
    public void Save_empty_list_clears_runners()
    {
        using var directory = new TestDirectory();
        var filePath = directory.File(CustomRunnerRepository.FileName);
        var sut = new CustomRunnerRepository(filePath);
        sut.Save([new Runner("old-id", "Old", isTemplate: true, FrameOrder.Ascending(2))]);
        sut.Save([]);
        Assert.Empty(new CustomRunnerRepository(filePath).Load());
        Assert.True(File.Exists(filePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{"id":"only-id"}]""")]
    [InlineData("""[null]""")]
    public void Load_returns_empty_list_for_corrupt_file(string json)
    {
        using var directory = new TestDirectory();
        var filePath = directory.File(CustomRunnerRepository.FileName);
        File.WriteAllText(filePath, json);
        Assert.Empty(new CustomRunnerRepository(filePath).Load());
    }
}
