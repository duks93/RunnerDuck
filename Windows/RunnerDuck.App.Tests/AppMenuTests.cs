using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tests;

public sealed class AppMenuTests
{
    [Fact]
    public void Reopening_reuses_thumbnails_and_updates_the_selected_runner() => OnSTA(() =>
    {
        using var fixture = new MenuFixture();
        fixture.Open();
        var original = fixture.Items;
        Assert.True(original[0].Checked);
        Assert.False(original[1].Checked);

        original[1].PerformClick();
        fixture.Open();

        Assert.Equal(fixture.Runners[1], fixture.CurrentRunner);
        Assert.Equal(2, fixture.Thumbnails.Count);
        Assert.Same(original[0], fixture.Items[0]);
        Assert.Same(original[1], fixture.Items[1]);
        Assert.False(original[0].Checked);
        Assert.True(original[1].Checked);
    });

    [Fact]
    public void Runner_addition_reordering_and_removal_refresh_the_menu() => OnSTA(() =>
    {
        using var fixture = new MenuFixture();
        fixture.Open();
        var previousItems = fixture.Items;
        var previousThumbnails = fixture.Thumbnails.ToArray();
        var custom = new Runner("custom", "Custom runner", false, FrameOrder.Ascending(2));
        fixture.Runners.Add(custom);
        fixture.Open();
        Assert.Equal(3, fixture.Items.Length);
        Assert.Equal("Custom runner", fixture.Items[2].Text);
        Assert.All(previousItems, item => Assert.Contains(item, fixture.DisposedItems));
        AssertDisposed(previousThumbnails);

        fixture.Runners.Reverse();
        fixture.Open();
        fixture.Items[0].PerformClick();
        Assert.Same(custom, fixture.CurrentRunner);

        fixture.Runners.Remove(custom);
        fixture.Open();
        Assert.Equal(2, fixture.Items.Length);
        Assert.DoesNotContain(fixture.Items, item => item.Text == "Custom runner");
    });

    [Theory]
    [InlineData("Renamed", false, 2)]
    [InlineData("Custom runner", true, 2)]
    [InlineData("Custom runner", false, 3)]
    public void Changed_runner_metadata_with_the_same_id_refreshes_its_thumbnail(string name, bool isTemplate, int frameCount) => OnSTA(() =>
    {
        using var fixture = new MenuFixture();
        fixture.Runners = [new Runner("custom", "Custom runner", false, FrameOrder.Ascending(2))];
        fixture.Open();
        var original = Assert.Single(fixture.Items);
        var updated = new Runner("custom", name, isTemplate, FrameOrder.Ascending(frameCount));
        fixture.Runners = [updated];
        fixture.Open();

        Assert.Contains(original, fixture.DisposedItems);
        Assert.Equal(2, fixture.Thumbnails.Count);
        var item = Assert.Single(fixture.Items);
        Assert.Equal(name, item.Text);
        item.PerformClick();
        Assert.Same(updated, fixture.CurrentRunner);
    });

    [Fact]
    public void Equivalent_reloaded_runner_profiles_reuse_the_menu() => OnSTA(() =>
    {
        using var fixture = new MenuFixture();
        fixture.Runners = [new Runner("custom", "Custom runner", false, FrameOrder.Ascending(2))];
        fixture.Open();
        var original = Assert.Single(fixture.Items);
        fixture.Runners = [new Runner("custom", "Custom runner", false, FrameOrder.Ascending(2))];
        fixture.Open();

        Assert.Single(fixture.Thumbnails);
        Assert.Same(original, Assert.Single(fixture.Items));
    });

    [Fact]
    public void Appearance_invalidation_replaces_thumbnails_and_dispose_releases_them() => OnSTA(() =>
    {
        var fixture = new MenuFixture();
        using (fixture)
        {
            fixture.Open();
            var previousItems = fixture.Items;
            var previousThumbnails = fixture.Thumbnails.ToArray();
            fixture.Menu.InvalidateRunnerThumbnails();
            fixture.Open();

            Assert.Equal(4, fixture.Thumbnails.Count);
            Assert.All(previousItems, item => Assert.Contains(item, fixture.DisposedItems));
            AssertDisposed(previousThumbnails);
            Assert.All(fixture.Items, item => Assert.NotNull(item.Image));
        }
        AssertDisposed(fixture.Thumbnails);
    });

    private static void AssertDisposed(IEnumerable<Bitmap> thumbnails)
    {
        foreach (var thumbnail in thumbnails)
        {
            Assert.Throws<ArgumentException>(() => thumbnail.GetPixel(0, 0));
        }
    }

    private static void OnSTA(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class MenuFixture : IDisposable
    {
        private readonly HashSet<ToolStripMenuItem> observedItems = [];

        public List<Runner> Runners { get; set; } = RunnerCatalog.BuiltInRunners.ToList();
        public Runner CurrentRunner { get; private set; } = Runner.Default;
        public List<Bitmap> Thumbnails { get; } = [];
        public HashSet<ToolStripMenuItem> DisposedItems { get; } = [];
        public AppMenu Menu { get; }
        public ToolStripMenuItem[] Items => ((ToolStripMenuItem)Menu.Menu.Items[0]).DropDownItems.Cast<ToolStripMenuItem>().ToArray();

        public MenuFixture()
        {
            Menu = new AppMenu("RunnerDuck", new AppMenuActions
            {
                Runners = () => Runners,
                CurrentRunner = () => CurrentRunner,
                SelectRunner = runner => CurrentRunner = runner,
                Thumbnail = _ =>
                {
                    var thumbnail = new Bitmap(16, 16);
                    Thumbnails.Add(thumbnail);
                    return thumbnail;
                },
                OpenTaskManager = () => { },
                OpenLicenses = () => { },
                ReportIssue = () => { },
                Quit = () => { },
            });
        }

        public void Open()
        {
            typeof(ToolStripDropDown)
                .GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Menu.Menu, [new CancelEventArgs()]);
            foreach (var item in Items)
            {
                if (observedItems.Add(item))
                {
                    item.Disposed += (_, _) => DisposedItems.Add(item);
                }
            }
        }

        public void Dispose() => Menu.Dispose();
    }
}
