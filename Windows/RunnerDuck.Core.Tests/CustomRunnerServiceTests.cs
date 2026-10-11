namespace RunnerDuck.Core.Tests;

public sealed class CustomRunnerServiceTests
{
    [Fact]
    public void Second_frame_write_failure_preserves_existing_runners_and_cleans_new_directory()
    {
        using var directory = new TestDirectory();
        var repository = new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName));
        var existing = new Runner("existing", "Existing", false, FrameOrder.Ascending(2));
        repository.Save([existing]);
        var writes = new List<string>();
        var deletes = new List<string>();
        var sut = new CustomRunnerService(repository, directory.Path, new CustomRunnerFileSystem
        {
            WriteFile = (path, _) =>
            {
                writes.Add(path);
                if (writes.Count == 2) throw new IOException("disk full");
            },
            DeleteDirectory = deletes.Add,
        }, () => FixedID);

        Assert.Equal(CustomRunnerError.SavingFailed,
            Assert.Throws<CustomRunnerException>(() => sut.Add("New", true, [[1], [2], [3]])).Error);
        Assert.Equal(new[] { "frame-0.png", "frame-1.png" }, writes.Select(Path.GetFileName));
        Assert.Equal(new[] { existing }, repository.Load());
        Assert.Equal(new[] { Path.Combine(directory.Path, FixedID.ToString().ToUpperInvariant()) }, deletes);
    }

    [Fact]
    public void Add_preserves_existing_list_and_exact_name()
    {
        var (sut, _, directory) = Make();
        using var cleanup = directory;
        var repository = new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName));
        var existing = new Runner("existing", "Bird", false, FrameOrder.Ascending(2));
        repository.Save([existing]);
        var added = sut.Add(" Bird ", true, [[1], [2]]);
        Assert.Equal(new RunnerSource.Custom(" Bird "), added.Source);
        Assert.Equal(new[] { existing, added }, repository.Load());
    }

    private static readonly Guid FixedID = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private sealed class FakeFiles
    {
        public Dictionary<string, byte[]> Written { get; } = [];
        public List<string> DeletedDirectories { get; } = [];
        public bool FailsWriting { get; set; }

        public CustomRunnerFileSystem FileSystem() => new()
        {
            WriteFile = (path, data) =>
            {
                if (FailsWriting)
                {
                    throw new IOException("disk full");
                }
                Written[path] = data;
            },
            DeleteDirectory = DeletedDirectories.Add,
        };
    }

    private static (CustomRunnerService Service, FakeFiles Files, TestDirectory Directory) Make()
    {
        var directory = new TestDirectory();
        var files = new FakeFiles();
        var service = new CustomRunnerService(
            new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName)),
            @"C:\Runners",
            files.FileSystem(),
            () => FixedID
        );
        return (service, files, directory);
    }

    [Fact]
    public void Add_saves_frames_in_order_and_appends_runner()
    {
        var (sut, files, directory) = Make();
        using var _ = directory;
        var runner = sut.Add("Bird", isTemplate: false, [[1], [2], [3]]);
        Assert.Equal("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", runner.ID);
        Assert.Equal(new RunnerSource.Custom("Bird"), runner.Source);
        Assert.False(runner.IsTemplate);
        Assert.Equal(new[] { 0, 1, 2 }, runner.FrameOrder.Order);
        Assert.Equal(new[] { "frame-0", "frame-1", "frame-2" }, runner.ResourceNames());
        Assert.Equal(
            new[] { 1, 2, 3 },
            Enumerable.Range(0, 3).Select(index => (int)files.Written[$@"C:\Runners\{runner.ID}\frame-{index}.png"][0])
        );
        Assert.Equal(new[] { runner.ID }, sut.Runners().Select(saved => saved.ID));
    }

    [Theory]
    [InlineData("", CustomRunnerError.NameEmpty)]
    [InlineData("   ", CustomRunnerError.NameEmpty)]
    [InlineData("Bird", CustomRunnerError.NameAlreadyExists)]
    public void Add_rejects_invalid_names(string name, CustomRunnerError expected)
    {
        var (sut, files, directory) = Make();
        using var _ = directory;
        sut.Add("Bird", isTemplate: true, [[1]]);
        files.Written.Clear();
        var error = Assert.Throws<CustomRunnerException>(() => sut.Add(name, isTemplate: true, [[1]]));
        Assert.Equal(expected, error.Error);
        Assert.Empty(files.Written);
        Assert.Single(sut.Runners());
    }

    [Fact]
    public void Name_check_is_case_sensitive_like_mac()
    {
        Assert.Null(CustomRunnerRules.ValidateName("bird", [new Runner("id", "Bird", true, FrameOrder.Ascending(1))]));
    }

    [Fact]
    public void Add_rejects_empty_and_too_many_frames()
    {
        var (sut, _, directory) = Make();
        using var __ = directory;
        Assert.Equal(CustomRunnerError.NoFrames, Assert.Throws<CustomRunnerException>(() => sut.Add("Empty", true, [])).Error);
        var tooMany = Enumerable.Repeat(new byte[] { 1 }, 31).ToList();
        Assert.Equal(CustomRunnerError.FrameLimitExceeded, Assert.Throws<CustomRunnerException>(() => sut.Add("Many", true, tooMany)).Error);
        Assert.Equal(30, sut.Add("Thirty", true, tooMany.Take(30).ToList()).FrameOrder.Order.Count);
    }

    [Fact]
    public void Add_failure_cleans_up_and_reports_saving_failed()
    {
        var (sut, files, directory) = Make();
        using var _ = directory;
        files.FailsWriting = true;
        var error = Assert.Throws<CustomRunnerException>(() => sut.Add("Bird", true, [[1]]));
        Assert.Equal(CustomRunnerError.SavingFailed, error.Error);
        Assert.Empty(sut.Runners());
        Assert.Equal(new[] { @"C:\Runners\AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE" }, files.DeletedDirectories);
    }

    [Fact]
    public void Delete_removes_runner_and_frames_but_not_the_runner_in_use()
    {
        var (sut, files, directory) = Make();
        using var _ = directory;
        var runner = sut.Add("Bird", true, [[1]]);
        var error = Assert.Throws<CustomRunnerException>(() => sut.Delete(runner, currentRunner: runner));
        Assert.Equal(CustomRunnerError.RunnerInUse, error.Error);
        Assert.Single(sut.Runners());
        sut.Delete(runner, currentRunner: Runner.Default);
        Assert.Empty(sut.Runners());
        Assert.Equal(new[] { $@"C:\Runners\{runner.ID}" }, files.DeletedDirectories);
    }

    [Theory]
    [InlineData(0, 2, new[] { "B", "C", "A" })]
    [InlineData(2, 0, new[] { "C", "A", "B" })]
    [InlineData(1, 5, new[] { "A", "B", "C" })]
    public void Move_reorders_saved_runners(int fromIndex, int toIndex, string[] expected)
    {
        using var directory = new TestDirectory();
        var repository = new CustomRunnerRepository(directory.File(CustomRunnerRepository.FileName));
        repository.Save(new[] { "A", "B", "C" }.Select(name => new Runner(name, name, true, FrameOrder.Ascending(1))));
        var sut = new CustomRunnerService(repository, @"C:\Runners", new FakeFiles().FileSystem(), Guid.NewGuid);
        sut.Move(fromIndex, toIndex);
        Assert.Equal(expected, sut.Runners().Select(runner => runner.ID));
    }

    [Theory]
    [InlineData(10, 36, true)]
    [InlineData(100, 36, true)]
    [InlineData(56, 36, true)]
    [InlineData(9, 36, false)]
    [InlineData(101, 36, false)]
    [InlineData(56, 35, false)]
    [InlineData(56, 72, false)]
    public void Frame_rules_match_mac_requirements(int width, int height, bool expected)
    {
        Assert.Equal(expected, CustomRunnerRules.IsValidFrame(width, height));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, false)]
    public void Frame_count_is_limited_to_thirty(int currentCount, bool expected)
    {
        Assert.Equal(expected, CustomRunnerRules.CanAddFrame(currentCount));
    }

    [Fact]
    public void Natural_sort_orders_numbered_frames_like_finder()
    {
        var names = new[] { "frame-10.png", "frame-02a.png", "frame-1.png", "frame-2.png", "frame-9.png" };
        Assert.Equal(
            new[] { "frame-1.png", "frame-2.png", "frame-02a.png", "frame-9.png", "frame-10.png" },
            names.OrderBy(name => name, NaturalStringComparer.Instance)
        );
    }

    [Fact]
    public void Natural_sort_breaks_ties_independently_of_input_order()
    {
        var expected = new[] { "Frame-2.png", "frame-02.png" };
        Assert.Equal(expected, new[] { "frame-02.png", "Frame-2.png" }.OrderBy(name => name, NaturalStringComparer.Instance));
        Assert.Equal(expected, new[] { "Frame-2.png", "frame-02.png" }.OrderBy(name => name, NaturalStringComparer.Instance));
    }
}
