/*
 SettingsForm.cs
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

using RunnerDuck.AIUsage;
using RunnerDuck.App.Properties;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunnerDuck.App.Settings;

internal sealed class SettingsForm : Form
{
    private const string RunnerGuideURL = "https://github.com/duks93/RunnerDuck/blob/main/docs/CustomRunners.md";

    private static readonly HttpClient UpdateHTTPClient = new();
    private readonly AppUpdateService appUpdateService = new(UpdateHTTPClient,
        AppUpdateService.CommitFromInformationalVersion(typeof(SettingsForm).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().SingleOrDefault()?.InformationalVersion));
    private readonly CancellationTokenSource updateCancellation = new();
    private readonly Button checkForUpdatesButton;
    private readonly Label appUpdateNote;
    private readonly LinkLabel updateChangesLink;
    private bool isCheckingForUpdates;
    private bool resourcesDisposed;
    private readonly ISettingsHost host;
    private readonly CustomMetricsSettingsControl customMetrics;
    private readonly ListBox customRunnerListBox = new() { DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false };
    private readonly CheckBox launchAtLoginCheckBox;
    private readonly Label launchAtLoginNote;
    private readonly ComboBox updateIntervalComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };
    private readonly ListBox runnerListBox = new() { DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false };
    private readonly ComboBox runnerSizeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };
    private readonly PictureBox previewBox = new() { SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle };
    private readonly FormsTimer previewTimer = new() { Interval = 120 };
    private readonly Dictionary<SystemMetricKind, CheckBox> monitoringCheckBoxes = [];
    private readonly CheckBox showsMetricsBarCheckBox;
    private readonly Dictionary<SystemMetricKind, CheckBox> metricsBarCheckBoxes = [];
    private readonly FlowLayoutPanel customMetricsBarPanel = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
    private readonly Dictionary<Guid, CheckBox> customMetricsBarCheckBoxes = [];
    private readonly FlowLayoutPanel aiUsagePanel = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
    private readonly Dictionary<Runner, Image?> thumbnails = [];
    private IReadOnlyList<Bitmap> previewFrames = [];
    private int previewIndex;
    private bool isRefreshing;

    public SettingsForm(ISettingsHost host, string appName)
    {
        this.host = host;
        Text = $"{Strings.Settings} - {appName}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(LogicalToDeviceUnits(560), LogicalToDeviceUnits(600));
        MinimumSize = new Size(LogicalToDeviceUnits(480), LogicalToDeviceUnits(420));
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var general = new SettingsPage();
        general.AddSection(Strings.Launch);
        launchAtLoginCheckBox = general.AddCheckBox(Strings.LaunchAtLogin, false, isOn => _ = ChangeLaunchAtLoginAsync(isOn));
        launchAtLoginNote = general.AddNote();
        general.AddSection(Strings.Monitoring);
        general.Add(new Label { Text = Strings.UpdateInterval, AutoSize = true });
        updateIntervalComboBox.Items.AddRange(Enum.GetValues<UpdateInterval>().Cast<object>().ToArray());
        updateIntervalComboBox.Format += (_, arguments) => arguments.Value = string.Format(Strings.Seconds, (int)(UpdateInterval)arguments.ListItem!);
        updateIntervalComboBox.SelectedIndexChanged += (_, _) => ChangeUpdateInterval();
        general.Add(updateIntervalComboBox);
        general.AddSection(Strings.AppUpdates);
        if (appUpdateService.InstalledCommit is { } commit)
        {
            general.AddNote($"{Strings.InstalledRevision}: {commit[..12]}");
        }
        appUpdateNote = general.AddNote(Strings.AppUpdateNotChecked);
        checkForUpdatesButton = general.Add(new Button { Text = Strings.CheckForUpdates, AutoSize = true });
        checkForUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        updateChangesLink = general.Add(new LinkLabel { Text = Strings.ViewUpdateChanges, AutoSize = true, Visible = false });
        updateChangesLink.LinkClicked += (_, _) => OpenURL(AppUpdateService.ChangesURL);
        tabs.TabPages.Add(Page(Strings.GeneralTab, general));

        var runner = new SettingsPage();
        runner.AddSection(Strings.Runner);
        runner.Add(RunnerPicker());
        runner.AddSection(Strings.Appearance);
        runner.Add(new Label { Text = Strings.RunnerSize, AutoSize = true });
        runnerSizeComboBox.AccessibleName = Strings.RunnerSize;
        runnerSizeComboBox.Items.AddRange(Enum.GetValues<RunnerSize>().Cast<object>().ToArray());
        runnerSizeComboBox.Format += (_, arguments) => arguments.Value = (RunnerSize)arguments.ListItem! switch
        {
            RunnerSize.Small => Strings.RunnerSizeSmall,
            RunnerSize.Large => Strings.RunnerSizeLarge,
            _ => Strings.RunnerSizeStandard,
        };
        runnerSizeComboBox.SelectedIndexChanged += (_, _) => ChangeRunnerSize();
        runner.Add(runnerSizeComboBox);
        runner.AddNote(Strings.RunnerSizeWindowsNote);
        runner.AddCheckBox(Strings.SlowDownUnderLoad, host.Settings.SpeedDecreasesUnderLoad, isOn =>
        {
            host.Settings.SpeedDecreasesUnderLoad = isOn;
            NotifyChange(SettingsChange.RunnerSpeed);
        });
        runner.AddCheckBox(Strings.FlipHorizontally, host.Settings.IsFlippedHorizontally, isOn =>
        {
            host.Settings.IsFlippedHorizontally = isOn;
            NotifyChange(SettingsChange.RunnerAppearance);
        });
        runner.AddSection(Strings.CustomRunners);
        runner.Add(CustomRunnerManager());
        var guideLink = new LinkLabel { Text = Strings.ViewRunnerGuide, AutoSize = true };
        guideLink.LinkClicked += (_, _) => OpenURL(RunnerGuideURL);
        runner.Add(guideLink);
        runner.AddNote(Strings.RunnerGuideDescription);
        tabs.TabPages.Add(Page(Strings.RunnerTab, runner));

        var metrics = new SettingsPage();
        metrics.AddSection(Strings.SystemMetrics);
        AddMonitoringCheckBox(metrics, SystemMetricKind.Memory, Strings.EnableMemoryUsageMonitoring);
        AddMonitoringCheckBox(metrics, SystemMetricKind.Storage, Strings.EnableStorageCapacityMonitoring);
        AddMonitoringCheckBox(metrics, SystemMetricKind.Battery, Strings.EnableBatteryStatusMonitoring);
        AddMonitoringCheckBox(metrics, SystemMetricKind.Network, Strings.EnableNetworkConnectivityMonitoring);
        metrics.AddSection(Strings.MetricsBar);
        showsMetricsBarCheckBox = metrics.AddCheckBox(Strings.ShowMetricsBar, host.Settings.ShowsMetricsBar, isOn =>
        {
            host.Settings.ShowsMetricsBar = isOn;
            NotifyChange(SettingsChange.MetricsBar);
        });
        metrics.AddNote(Strings.MetricsBarWindowsNote);
        AddMetricsBarCheckBox(metrics, SystemMetricKind.CPU, Strings.CPU);
        AddMetricsBarCheckBox(metrics, SystemMetricKind.Memory, Strings.Memory);
        AddMetricsBarCheckBox(metrics, SystemMetricKind.Storage, Strings.Storage);
        AddMetricsBarCheckBox(metrics, SystemMetricKind.Battery, Strings.Battery);
        AddMetricsBarCheckBox(metrics, SystemMetricKind.Network, Strings.Network);
        metrics.Add(customMetricsBarPanel);
        metrics.AddSection(Strings.CustomMetrics);
        customMetrics = new CustomMetricsSettingsControl(host);
        metrics.Add(customMetrics);
        metrics.AddNote(Strings.CustomMetricsDescription);
        var schemaLink = new LinkLabel { Text = Strings.ViewJSONSchemaAndSamples, AutoSize = true };
        schemaLink.LinkClicked += (_, _) => OpenURL("https://github.com/duks93/RunnerDuck/blob/main/docs/CustomMetricsSchema.md");
        metrics.Add(schemaLink);
        metrics.AddSection(Strings.AIUsage);
        metrics.Add(aiUsagePanel);
        metrics.AddNote(Strings.AIUsageSettingsFooter);
        tabs.TabPages.Add(Page(Strings.MetricsTab, metrics));

        Controls.Add(tabs);
        previewTimer.Tick += (_, _) => AdvancePreview();
    }

    public void Present()
    {
        ReloadSettings();
        if (!Visible)
        {
            Show();
        }
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
    }

    public void RefreshCustomMetricsSources()
    {
        customMetrics.RefreshSources();
        RefreshMetricsBarSelections();
    }

    protected override void OnFormClosing(FormClosingEventArgs arguments)
    {
        if (arguments.CloseReason == CloseReason.UserClosing)
        {
            arguments.Cancel = true;
            Hide();
            previewTimer.Stop();
            return;
        }
        base.OnFormClosing(arguments);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            updateCancellation.Cancel();
            updateCancellation.Dispose();
            previewTimer.Dispose();
            DisposePreviewFrames();
            DisposeThumbnails();
        }
        base.Dispose(disposing);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (isCheckingForUpdates) return;
        isCheckingForUpdates = true;
        checkForUpdatesButton.Enabled = false;
        updateChangesLink.Visible = false;
        appUpdateNote.Text = Strings.AppUpdateChecking;
        AppUpdateStatus status;
        try
        {
            status = await appUpdateService.CheckAsync(updateCancellation.Token);
        }
        catch (Exception exception)
        {
            Log.Write($"Update check failed: {exception.GetType().Name}");
            status = AppUpdateStatus.Failed;
        }
        if (resourcesDisposed || IsDisposed || Disposing) return;
        appUpdateNote.Text = status switch
        {
            AppUpdateStatus.UpToDate => Strings.AppUpdateUpToDate,
            AppUpdateStatus.Available => Strings.AppUpdateAvailable,
            AppUpdateStatus.UnpublishedBuild => Strings.AppUpdateUnpublishedBuild,
            AppUpdateStatus.UnknownBuild => Strings.AppUpdateUnknownBuild,
            _ => Strings.AppUpdateFailed,
        };
        updateChangesLink.Visible = status == AppUpdateStatus.Available;
        isCheckingForUpdates = false;
        checkForUpdatesButton.Enabled = true;
    }

    private static TabPage Page(string title, SettingsPage content)
    {
        var page = new TabPage(title) { UseVisualStyleBackColor = true };
        page.Controls.Add(content);
        return page;
    }

    private Control RunnerPicker()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        runnerListBox.ItemHeight = LogicalToDeviceUnits(28);
        runnerListBox.Height = LogicalToDeviceUnits(28) * 8 + 4;
        runnerListBox.Dock = DockStyle.Fill;
        runnerListBox.DrawItem += DrawRunnerItem;
        runnerListBox.SelectedIndexChanged += (_, _) => ChangeRunner();
        previewBox.Size = new Size(LogicalToDeviceUnits(140), LogicalToDeviceUnits(84));
        previewBox.Margin = new Padding(LogicalToDeviceUnits(12), 0, 0, 0);
        layout.Controls.Add(runnerListBox, 0, 0);
        layout.SetRowSpan(runnerListBox, 2);
        layout.Controls.Add(new Label { Text = Strings.Preview, AutoSize = true, Margin = new Padding(LogicalToDeviceUnits(12), 0, 0, LogicalToDeviceUnits(4)) }, 1, 0);
        layout.Controls.Add(previewBox, 1, 1);
        return layout;
    }

    private Control CustomRunnerManager()
    {
        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        customRunnerListBox.ItemHeight = LogicalToDeviceUnits(28);
        customRunnerListBox.Height = LogicalToDeviceUnits(28) * 4 + 4;
        customRunnerListBox.Dock = DockStyle.Fill;
        customRunnerListBox.DrawItem += DrawRunnerItem;
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(LogicalToDeviceUnits(12), 0, 0, 0) };
        buttons.Controls.AddRange([
            ActionButton(Strings.AddCustomRunner, AddCustomRunner),
            ActionButton(Strings.Remove, DeleteCustomRunner),
            ActionButton(Strings.MoveUp, () => MoveCustomRunner(-1)),
            ActionButton(Strings.MoveDown, () => MoveCustomRunner(1)),
        ]);
        layout.Controls.Add(customRunnerListBox, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        return layout;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Dock = DockStyle.Top };
        button.Click += (_, _) => action();
        return button;
    }

    private void AddCustomRunner()
    {
        using var editor = new CustomRunnerEditorForm(host);
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        ReloadSettings();
        if (editor.AddedRunner is { } addedRunner)
        {
            customRunnerListBox.SelectedItem = addedRunner;
        }
    }

    private void DeleteCustomRunner()
    {
        if (customRunnerListBox.SelectedItem is not Runner runner)
        {
            return;
        }
        try
        {
            host.DeleteCustomRunner(runner);
            ReloadSettings();
        }
        catch (CustomRunnerException exception)
        {
            MessageBox.Show(this, CustomRunnerMessages.Message(exception.Error), Strings.CustomRunners, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MoveCustomRunner(int offset)
    {
        var index = customRunnerListBox.SelectedIndex;
        if (index < 0 || index + offset < 0 || index + offset >= customRunnerListBox.Items.Count)
        {
            return;
        }
        var runner = customRunnerListBox.SelectedItem;
        try
        {
            host.MoveCustomRunner(index, index + offset);
            ReloadSettings();
            customRunnerListBox.SelectedItem = runner;
        }
        catch (CustomRunnerException exception)
        {
            MessageBox.Show(this, CustomRunnerMessages.Message(exception.Error), Strings.CustomRunners, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenURL(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Write($"Opening {url} failed: {exception.Message}");
        }
    }

    private void AddMonitoringCheckBox(SettingsPage page, SystemMetricKind kind, string text)
    {
        monitoringCheckBoxes[kind] = page.AddCheckBox(text, SettingsEditor.IsMonitored(host.Settings, kind), isOn =>
        {
            if (isRefreshing) return;
            SettingsEditor.SetMonitoring(host.Settings, kind, isOn);
            NotifyChange(SettingsChange.Monitoring);
            RefreshMetricsBarSelections();
        });
    }

    private void AddMetricsBarCheckBox(SettingsPage page, SystemMetricKind kind, string text)
    {
        metricsBarCheckBoxes[kind] = page.AddCheckBox(text, SettingsEditor.ShowsInMetricsBar(host.Settings, kind), isOn =>
        {
            if (isRefreshing) return;
            var monitoringChanged = SettingsEditor.SetShowsInMetricsBar(host.Settings, kind, isOn);
            NotifyChange(monitoringChanged ? SettingsChange.Monitoring : SettingsChange.MetricsBar);
            RefreshMetricsBarSelections();
        });
    }

    private void RefreshMetricsBarSelections()
    {
        var wasRefreshing = isRefreshing;
        isRefreshing = true;
        try
        {
            foreach (var (kind, checkBox) in metricsBarCheckBoxes)
            {
                checkBox.Checked = SettingsEditor.ShowsInMetricsBar(host.Settings, kind);
            }
            foreach (var (kind, checkBox) in monitoringCheckBoxes)
            {
                checkBox.Checked = SettingsEditor.IsMonitored(host.Settings, kind);
            }
            var sources = host.CustomMetricsSources();
            var sourceIDs = sources.Select(source => source.ID).ToHashSet();
            foreach (var id in customMetricsBarCheckBoxes.Keys.Where(id => !sourceIDs.Contains(id)).ToArray())
            {
                customMetricsBarCheckBoxes[id].Dispose();
                customMetricsBarCheckBoxes.Remove(id);
            }
            foreach (var source in sources)
            {
                if (!customMetricsBarCheckBoxes.TryGetValue(source.ID, out var checkBox))
                {
                    checkBox = new CheckBox { AutoSize = true };
                    var sourceID = source.ID;
                    var sourceCheckBox = checkBox;
                    checkBox.CheckedChanged += (_, _) =>
                    {
                        if (isRefreshing) return;
                        SettingsEditor.SetShowsCustomMetricsInMetricsBar(host.Settings, sourceID, sourceCheckBox.Checked);
                        NotifyChange(SettingsChange.MetricsBar);
                    };
                    customMetricsBarCheckBoxes.Add(sourceID, checkBox);
                    customMetricsBarPanel.Controls.Add(checkBox);
                }
                checkBox.Text = source.DisplayName;
                checkBox.Checked = host.Settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs.Contains(source.ID);
            }
        }
        finally { isRefreshing = wasRefreshing; }
    }

    private void ReloadSettings()
    {
        isRefreshing = true;
        try
        {
            var settings = host.Settings;
            updateIntervalComboBox.SelectedItem = settings.UpdateInterval;
            runnerSizeComboBox.SelectedItem = settings.RunnerSize;
            foreach (var (kind, checkBox) in monitoringCheckBoxes)
            {
                checkBox.Checked = SettingsEditor.IsMonitored(settings, kind);
            }
            showsMetricsBarCheckBox.Checked = settings.ShowsMetricsBar;
            RefreshRunners();
            RefreshAIUsageSources();
            customMetrics.RefreshSources();
            RefreshMetricsBarSelections();
        }
        finally
        {
            isRefreshing = false;
        }
        _ = RefreshLaunchAtLoginAsync();
    }

    private void RefreshRunners()
    {
        DisposeThumbnails();
        runnerListBox.Items.Clear();
        foreach (var runner in host.Runners())
        {
            thumbnails[runner] = host.Thumbnail(runner);
            runnerListBox.Items.Add(runner);
        }
        runnerListBox.SelectedItem = host.CurrentRunner;
        customRunnerListBox.Items.Clear();
        foreach (var customRunner in host.CustomRunners())
        {
            customRunnerListBox.Items.Add(customRunner);
        }
        StartPreview(host.CurrentRunner);
    }

    private void RefreshAIUsageSources()
    {
        aiUsagePanel.Controls.Clear();
        foreach (var state in host.AIUsageSourceStates())
        {
            var detection = state.IsDetected ? Strings.AIUsageDetected : Strings.AIUsageNotDetected;
            var checkBox = new CheckBox
            {
                Text = $"{SourceName(state.Source)} ({detection})",
                Checked = state.IsEnabled,
                AutoSize = true,
                Margin = new Padding(0, LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(2)),
            };
            var source = state.Source;
            checkBox.CheckedChanged += (_, _) =>
            {
                host.Settings.AIUsageConfiguration.Set(source, checkBox.Checked);
                NotifyChange(SettingsChange.AIUsageSources);
            };
            aiUsagePanel.Controls.Add(checkBox);
        }
    }

    private async Task RefreshLaunchAtLoginAsync() => ShowLaunchAtLogin(await LaunchAtLogin.StateAsync());

    private async Task ChangeLaunchAtLoginAsync(bool isOn)
    {
        if (isRefreshing)
        {
            return;
        }
        launchAtLoginCheckBox.Enabled = false;
        ShowLaunchAtLogin(await LaunchAtLogin.SetAsync(isOn));
    }

    private void ShowLaunchAtLogin(LaunchAtLoginState state)
    {
        isRefreshing = true;
        try
        {
            launchAtLoginCheckBox.Checked = state.IsOn();
            launchAtLoginCheckBox.Enabled = state.CanChange();
            launchAtLoginNote.Text = state.Note() switch
            {
                LaunchAtLoginNote.TurnOnInTaskManager => Strings.LaunchAtLoginTurnOnInTaskManager,
                LaunchAtLoginNote.ManagedByPolicy => Strings.LaunchAtLoginManagedByPolicy,
                LaunchAtLoginNote.Unavailable => Strings.LaunchAtLoginUnavailable,
                _ => "",
            };
            launchAtLoginNote.Visible = launchAtLoginNote.Text.Length > 0;
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void ChangeUpdateInterval()
    {
        if (isRefreshing || updateIntervalComboBox.SelectedItem is not UpdateInterval interval)
        {
            return;
        }
        host.Settings.UpdateInterval = interval;
        host.SettingsChanged(SettingsChange.UpdateInterval);
    }

    private void ChangeRunner()
    {
        if (isRefreshing || runnerListBox.SelectedItem is not Runner runner || runner == host.CurrentRunner)
        {
            return;
        }
        host.SelectRunner(runner);
        isRefreshing = true;
        try
        {
            runnerListBox.SelectedItem = host.CurrentRunner;
        }
        finally
        {
            isRefreshing = false;
        }
        StartPreview(host.CurrentRunner);
    }

    private void ChangeRunnerSize()
    {
        if (isRefreshing || runnerSizeComboBox.SelectedItem is not RunnerSize size)
        {
            return;
        }
        host.Settings.RunnerSize = size;
        NotifyChange(SettingsChange.RunnerAppearance);
    }

    private void NotifyChange(SettingsChange change)
    {
        if (!isRefreshing)
        {
            host.SettingsChanged(change);
            if (change == SettingsChange.RunnerAppearance)
            {
                StartPreview(host.CurrentRunner);
            }
        }
    }

    private void DrawRunnerItem(object? sender, DrawItemEventArgs arguments)
    {
        arguments.DrawBackground();
        if (sender is not ListBox listBox || arguments.Index < 0 || listBox.Items[arguments.Index] is not Runner runner)
        {
            return;
        }
        var bounds = arguments.Bounds;
        var iconSize = LogicalToDeviceUnits(20);
        if (thumbnails.GetValueOrDefault(runner) is { } thumbnail)
        {
            arguments.Graphics.DrawImage(thumbnail, bounds.Left + LogicalToDeviceUnits(6), bounds.Top + (bounds.Height - iconSize) / 2, iconSize, iconSize);
        }
        var textBounds = new Rectangle(bounds.Left + iconSize + LogicalToDeviceUnits(14), bounds.Top, bounds.Width - iconSize - LogicalToDeviceUnits(14), bounds.Height);
        TextRenderer.DrawText(
            arguments.Graphics,
            RunnerNames.Name(runner),
            arguments.Font,
            textBounds,
            arguments.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );
        arguments.DrawFocusRectangle();
    }

    private void StartPreview(Runner runner)
    {
        DisposePreviewFrames();
        var color = WindowsTheme.AppsUseLightTheme() ? Color.Black : Color.White;
        previewBox.BackColor = WindowsTheme.AppsUseLightTheme() ? Color.White : Color.FromArgb(32, 32, 32);
        previewFrames = host.PreviewFrames(runner, LogicalToDeviceUnits(48), color);
        previewIndex = 0;
        previewBox.Image = previewFrames.FirstOrDefault();
        previewTimer.Start();
    }

    private void AdvancePreview()
    {
        if (previewFrames.Count == 0)
        {
            return;
        }
        previewIndex = (previewIndex + 1) % previewFrames.Count;
        previewBox.Image = previewFrames[previewIndex];
    }

    private void DisposePreviewFrames()
    {
        previewBox.Image = null;
        foreach (var frame in previewFrames.Distinct())
        {
            frame.Dispose();
        }
        previewFrames = [];
    }

    private void DisposeThumbnails()
    {
        foreach (var thumbnail in thumbnails.Values)
        {
            thumbnail?.Dispose();
        }
        thumbnails.Clear();
    }

    private static string SourceName(AIUsageSource source) => source switch
    {
        AIUsageSource.ClaudeCode => "Claude Code",
        AIUsageSource.Codex => "Codex",
        AIUsageSource.Grok => "Grok",
        AIUsageSource.OpenCodex => "opencodex",
        _ => source.ToString(),
    };
}
