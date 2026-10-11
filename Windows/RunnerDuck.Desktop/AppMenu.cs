/*
 AppMenu.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

using RunnerDuck.App.Properties;
using RunnerDuck.Core;

namespace RunnerDuck.App;

internal sealed class AppMenuActions
{
    public required Func<IReadOnlyList<Runner>> Runners { get; init; }
    public required Func<Runner> CurrentRunner { get; init; }
    public required Action<Runner> SelectRunner { get; init; }
    public required Func<Runner, Image?> Thumbnail { get; init; }
    public Action? OpenSettings { get; init; }
    public required Action OpenTaskManager { get; init; }
    public required Action OpenLicenses { get; init; }
    public required Action ReportIssue { get; init; }
    public required Action Quit { get; init; }
}

internal sealed class AppMenu : IDisposable
{
    private readonly AppMenuActions actions;
    private readonly ToolStripMenuItem runnerMenu = new(Strings.Runner);
    private readonly List<Image> thumbnails = [];
    private IReadOnlyList<Runner> menuRunners = [];
    private bool runnerThumbnailsInvalidated = true;

    public AppMenu(string appName, AppMenuActions actions)
    {
        this.actions = actions;
        Menu.Items.Add(runnerMenu);
        Menu.Items.Add(new ToolStripSeparator());
        if (actions.OpenSettings is { } openSettings)
        {
            Menu.Items.Add(Strings.Settings, null, (_, _) => openSettings());
        }
        Menu.Items.Add(Strings.OpenTaskManager, null, (_, _) => actions.OpenTaskManager());
        Menu.Items.Add(new ToolStripSeparator());
        Menu.Items.Add(Strings.OpenSourceLicense, null, (_, _) => actions.OpenLicenses());
        Menu.Items.Add(Strings.ReportIssue, null, (_, _) => actions.ReportIssue());
        Menu.Items.Add(string.Format(Strings.Quit, appName), null, (_, _) => actions.Quit());
        Menu.Opening += (_, _) => RefreshRunnerMenu();
    }

    public ContextMenuStrip Menu { get; } = new();

    public void InvalidateRunnerThumbnails() => runnerThumbnailsInvalidated = true;

    public void Dispose()
    {
        DisposeRunnerItems();
        Menu.Dispose();
    }

    private void RefreshRunnerMenu()
    {
        var runners = actions.Runners();
        if (runnerThumbnailsInvalidated || !HasSameRunners(runners))
        {
            RebuildRunnerMenu(runners);
        }
        var currentRunner = actions.CurrentRunner();
        for (var index = 0; index < menuRunners.Count; index++)
        {
            ((ToolStripMenuItem)runnerMenu.DropDownItems[index]).Checked = menuRunners[index] == currentRunner;
        }
    }

    private bool HasSameRunners(IReadOnlyList<Runner> runners) =>
        menuRunners.Count == runners.Count && menuRunners.Zip(runners).All(pair =>
            pair.First == pair.Second && pair.First.Source == pair.Second.Source
            && pair.First.IsTemplate == pair.Second.IsTemplate
            && pair.First.FrameOrder.Order.SequenceEqual(pair.Second.FrameOrder.Order));

    private void RebuildRunnerMenu(IReadOnlyList<Runner> runners)
    {
        DisposeRunnerItems();
        foreach (var runner in runners)
        {
            var thumbnail = actions.Thumbnail(runner);
            if (thumbnail is not null)
            {
                thumbnails.Add(thumbnail);
            }
            var item = new ToolStripMenuItem(RunnerNames.Name(runner), thumbnail, (_, _) => actions.SelectRunner(runner));
            runnerMenu.DropDownItems.Add(item);
        }
        menuRunners = runners.ToArray();
        runnerThumbnailsInvalidated = false;
    }

    private void DisposeRunnerItems()
    {
        while (runnerMenu.DropDownItems.Count > 0)
        {
            var item = runnerMenu.DropDownItems[0];
            runnerMenu.DropDownItems.RemoveAt(0);
            item.Dispose();
        }
        thumbnails.ForEach(thumbnail => thumbnail.Dispose());
        thumbnails.Clear();
        menuRunners = [];
    }
}

internal static class RunnerNames
{
    public static string Name(Runner runner) => runner.Source switch
    {
        RunnerSource.Custom custom => custom.Name,
        RunnerSource.BuiltIn { Kind: RunnerKind.Duck } => Strings.RunnerDuck,
        RunnerSource.BuiltIn { Kind: RunnerKind.DuckFilled } => Strings.RunnerDuckFilled,
        _ => runner.ID,
    };
}
