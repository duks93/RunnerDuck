/*
 RunnerDuckApplicationContext.cs
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

using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using RunnerDuck.AIUsage;
using RunnerDuck.AIUsage.Presentation;
using RunnerDuck.App.Dashboard;
using RunnerDuck.App.Properties;
using RunnerDuck.App.Settings;
using RunnerDuck.App.SystemMetrics;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunnerDuck.App;

internal sealed class RunnerDuckApplicationContext : ApplicationContext, ISettingsHost
{
    private const string AppName = "RunnerDuck";
    private const string IssuesURL = "https://github.com/duks93/RunnerDuck/issues";

    private readonly AppSettingsRepository settingsRepository = new(AppPaths.SettingsFile);
    private readonly CustomRunnerRepository customRunnerRepository = new(AppPaths.CustomRunnersFile);
    private readonly RunnerFrameStore frameStore = new();
    private readonly CustomRunnerService customRunnerService;
    private readonly SystemMetricsMonitor metricsMonitor = new();
    private readonly AIUsageService aiUsageService = new(AIUsageEnvironment.Live(Log.Write));
    private readonly AIUsageMonitor aiUsageMonitor;
    private readonly FormsTimer aiUsageTimer = new() { Interval = (int)AIUsageService.RefreshInterval.TotalMilliseconds };
    private readonly CustomMetricsService customMetricsService = new(CustomMetricsEnvironment.Live(Log.Write));
    private readonly SynchronizationContext uiContext;
    private readonly FormsTimer animationTimer = new();
    private readonly RunnerTrayIcon runnerTrayIcon;
    private readonly MetricsTrayIcons metricsTrayIcons;
    private readonly AppMenu appMenu;
    private readonly DashboardForm dashboard;
    private RunnerAnimationPlayer? duckPlayer;
    private double lastAnimationTime;
    private AppSettings settings;
    private Runner runner = Runner.Default;
    private IReadOnlyList<Bitmap> sourceFrames = [];
    private LicensesForm? licensesForm;
    private SettingsForm? settingsForm;

    public RunnerDuckApplicationContext()
    {
        settings = settingsRepository.Load();
        customRunnerService = new CustomRunnerService(customRunnerRepository, AppPaths.CustomRunnersDirectory, CustomRunnerFileSystem.Live, Guid.NewGuid);
        appMenu = new AppMenu(AppName, new AppMenuActions
        {
            Runners = () => RunnerCatalog.All(customRunnerRepository.Load()),
            CurrentRunner = () => runner,
            SelectRunner = SelectRunner,
            Thumbnail = Thumbnail,
            OpenSettings = OpenSettings,
            OpenTaskManager = OpenTaskManager,
            OpenLicenses = OpenLicenses,
            ReportIssue = () => OpenURL(IssuesURL),
            Quit = ExitThread,
        });
        // Keep the runner as the first NotifyIcon: its existing Shell identity uses ID 1.
        runnerTrayIcon = new RunnerTrayIcon(appMenu.Menu);
        metricsTrayIcons = new MetricsTrayIcons(appMenu.Menu, ToggleDashboard, MetricTitle);
        dashboard = new DashboardForm(AppName, appMenu.Menu);
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        aiUsageMonitor = new AIUsageMonitor(AppPaths.SettingsFile, aiUsageService.RefreshAsync, Log.Write, Timeout.InfiniteTimeSpan);
        aiUsageMonitor.Updated += OnAIUsageUpdated;
        customMetricsService.BundlesChanged += OnCustomMetricsChanged;
        runnerTrayIcon.LeftClicked += ToggleDashboard;
        try
        {
            ApplySelection(RunnerSelection.Select(settings, customRunnerRepository.Load(), frameStore.LoadFrames));
        }
        catch (RunnerLoadingException exception)
        {
            Log.Write($"Loading the default runner failed: {exception.Message}");
            using var logo = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.png"));
            runnerTrayIcon.SetFrames([logo]);
        }
        runnerTrayIcon.SetToolTip(AppName);
        runnerTrayIcon.Show();
        animationTimer.Tick += (_, _) => Animate();
        metricsMonitor.Updated += OnMetricsUpdated;
        UpdateAnimationInterval(null);
        StartMonitoring();
        aiUsageTimer.Tick += (_, _) => { _ = Task.Run(aiUsageMonitor.RefreshAsync); };
        aiUsageTimer.Start();
        _ = Task.Run(aiUsageMonitor.RefreshAsync);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            animationTimer.Dispose();
            aiUsageTimer.Dispose();
            aiUsageMonitor.Updated -= OnAIUsageUpdated;
            aiUsageMonitor.Dispose();
            metricsMonitor.Dispose();
            customMetricsService.Dispose();
            dashboard.Dispose();
            licensesForm?.Dispose();
            settingsForm?.Dispose();
            runnerTrayIcon.Dispose();
            metricsTrayIcons.Dispose();
            appMenu.Dispose();
            DisposeSourceFrames();
        }
        base.Dispose(disposing);
    }

    AppSettings ISettingsHost.Settings => settings;

    Runner ISettingsHost.CurrentRunner => runner;

    IReadOnlyList<Runner> ISettingsHost.Runners() => RunnerCatalog.All(customRunnerRepository.Load());

    void ISettingsHost.SelectRunner(Runner newRunner) => SelectRunner(newRunner);

    Image? ISettingsHost.Thumbnail(Runner thumbnailRunner) => Thumbnail(thumbnailRunner);

    IReadOnlyList<Bitmap> ISettingsHost.PreviewFrames(Runner previewRunner, int height, Color color)
    {
        IReadOnlyList<Bitmap> frames;
        try
        {
            frames = frameStore.LoadFrames(previewRunner);
        }
        catch (RunnerLoadingException exception)
        {
            Log.Write(exception.Message);
            return [];
        }
        var bounds = BitmapExtensions.OpaqueBounds(frames.Distinct());
        IEnumerable<Bitmap> animationFrames = previewRunner.Source is RunnerSource.BuiltIn builtIn
            ? DuckAssets.Animation(builtIn.Kind).Clips["walk"].Frames.Select(index => frames[index])
            : frames;
        var previews = animationFrames
            .Select(frame =>
            {
                using var cropped = frame.Crop(bounds);
                using var fitted = cropped.FitToSquare(height, settings.IsFlippedHorizontally, settings.RunnerSize.Scale());
                return previewRunner.IsTemplate ? fitted.Recolor(color) : new Bitmap(fitted);
            })
            .ToList();
        foreach (var frame in frames.Distinct())
        {
            frame.Dispose();
        }
        return previews;
    }

    IReadOnlyList<AIUsageSourceState> ISettingsHost.AIUsageSourceStates() =>
        aiUsageService.SourceStates(settings.AIUsageConfiguration);

    IReadOnlyList<Runner> ISettingsHost.CustomRunners() => customRunnerService.Runners();

    Runner ISettingsHost.AddCustomRunner(string name, bool isTemplate, IReadOnlyList<byte[]> pngFrames) =>
        customRunnerService.Add(name, isTemplate, pngFrames);

    void ISettingsHost.DeleteCustomRunner(Runner customRunner) => customRunnerService.Delete(customRunner, runner);

    void ISettingsHost.MoveCustomRunner(int fromIndex, int toIndex) => customRunnerService.Move(fromIndex, toIndex);

    IReadOnlyList<CustomMetricsSource> ISettingsHost.CustomMetricsSources() => settings.CustomMetricsConfiguration.Sources;

    bool ISettingsHost.IsCustomMetricsSourceFailed(Guid sourceID) =>
        customMetricsService.Bundles.Any(bundle => bundle.ID == sourceID && bundle.IsFailed);

    void ISettingsHost.AddCustomMetricsSource(string filePath)
    {
        var source = customMetricsService.CreateSource(filePath);
        ApplyCustomMetricsChange(current => SettingsEditor.AddCustomMetricsSource(current, source));
    }

    void ISettingsHost.RemoveCustomMetricsSource(Guid sourceID)
    {
        ApplyCustomMetricsChange(current => SettingsEditor.RemoveCustomMetricsSource(current, sourceID));
    }

    void ISettingsHost.MoveCustomMetricsSource(int fromIndex, int toIndex)
    {
        ApplyCustomMetricsChange(current => SettingsEditor.MoveCustomMetricsSource(current, fromIndex, toIndex));
    }

    private void ApplyCustomMetricsChange(Action<AppSettings> change)
    {
        SettingsEditor.SaveCustomMetricsChange(settings, change, settingsRepository.Save);
        customMetricsService.Reconcile(settings.CustomMetricsConfiguration.Sources);
    }

    void ISettingsHost.SettingsChanged(SettingsChange change)
    {
        settingsRepository.Save(settings);
        switch (change)
        {
            case SettingsChange.UpdateInterval:
                metricsMonitor.Stop();
                StartMonitoring();
                break;
            case SettingsChange.Monitoring:
                metricsMonitor.Configure(settings.SystemMetricsConfiguration);
                RenderMetricsBar();
                break;
            case SettingsChange.RunnerAppearance:
                RenderIcons();
                break;
            case SettingsChange.RunnerSpeed:
                UpdateAnimationInterval(metricsMonitor.Metrics.SystemInfoBundle.CPUInfo);
                break;
            case SettingsChange.MetricsBar:
                RenderMetricsBar();
                break;
            case SettingsChange.AIUsageSources:
                _ = Task.Run(aiUsageMonitor.ReloadConfigurationAsync);
                break;
        }
    }

    private void OpenSettings()
    {
        dashboard.Hide();
        if (settingsForm is null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm(this, AppName);
        }
        settingsForm.Present();
    }

    private void SelectRunner(Runner newRunner)
    {
        try
        {
            ApplySelection(RunnerSelection.Switch(newRunner, settings, frameStore.LoadFrames));
        }
        catch (RunnerLoadingException exception)
        {
            Log.Write(exception.Message);
            runnerTrayIcon.ShowBalloon(AppName, Strings.RunnerLoadingFailed);
        }
    }

    private void ApplySelection(RunnerSelectionResult<IReadOnlyList<Bitmap>> selection)
    {
        if (selection.Failure is { } failure)
        {
            Log.Write(failure.Message);
            runnerTrayIcon.ShowBalloon(AppName, Strings.RunnerLoadingFailed);
        }
        DisposeSourceFrames();
        var changedRunner = runner.ID != selection.Runner.ID;
        runner = selection.Runner;
        if (runner.Source is not RunnerSource.BuiltIn builtIn) duckPlayer = null;
        else if (changedRunner || duckPlayer is null) duckPlayer = new RunnerAnimationPlayer(DuckAssets.Animation(builtIn.Kind));
        sourceFrames = selection.Frames;
        settingsRepository.Save(settings);
        RenderIcons();
        if (changedRunner && runner.IsCustom) runnerTrayIcon.SelectFrame(0);
        UpdateAnimationInterval(metricsMonitor.Metrics.SystemInfoBundle.CPUInfo);
    }

    private void RenderIcons()
    {
        if (sourceFrames.Count == 0)
        {
            return;
        }
        var size = SystemInformation.SmallIconSize.Width;
        var color = WindowsTheme.TaskbarContrastColor();
        var bounds = BitmapExtensions.OpaqueBounds(sourceFrames.Distinct());
        var renderedFrames = sourceFrames
            .Select(frame =>
            {
                using var cropped = frame.Crop(bounds);
                using var fitted = cropped.FitToSquare(size, settings.IsFlippedHorizontally, settings.RunnerSize.Scale());
                return runner.IsTemplate ? fitted.Recolor(color) : new Bitmap(fitted);
            })
            .ToList();
        runnerTrayIcon.SetFrames(renderedFrames);
        if (duckPlayer is not null) runnerTrayIcon.SelectFrame(duckPlayer.FrameIndex);
        renderedFrames.ForEach(frame => frame.Dispose());
    }

    private Image? Thumbnail(Runner thumbnailRunner)
    {
        using var frame = frameStore.LoadThumbnail(thumbnailRunner);
        if (frame is null)
        {
            return null;
        }
        using var cropped = frame.Crop(BitmapExtensions.OpaqueBounds([frame]));
        using var fitted = cropped.FitToSquare(LogicalSize(16), flipsHorizontally: false);
        return thumbnailRunner.IsTemplate
            ? fitted.Recolor(WindowsTheme.AppsUseLightTheme() ? Color.Black : Color.White)
            : new Bitmap(fitted);
    }

    private void StartMonitoring()
    {
        metricsMonitor.Configure(settings.SystemMetricsConfiguration);
        metricsMonitor.Start(settings.UpdateInterval);
        customMetricsService.Reconcile(settings.CustomMetricsConfiguration.Sources);
        lastAnimationTime = NowSeconds;
        animationTimer.Start();
    }

    private void StopMonitoring()
    {
        metricsMonitor.Stop();
        customMetricsService.Stop();
        animationTimer.Stop();
    }

    private void RenderDashboard() => dashboard.Render(metricsMonitor.Metrics, customMetricsService.Bundles, aiUsageMonitor.Snapshot.Bundles, DateTimeOffset.Now);

    private void RenderMetricsBar() => metricsTrayIcons.Update(
        settings.ShowsMetricsBar
            ? MetricsBarItems.Build(settings.MetricsBarConfiguration, metricsMonitor.Metrics.SystemInfoBundle, customMetricsService.Bundles)
            : [],
        SystemInformation.SmallIconSize.Width,
        WindowsTheme.TaskbarContrastColor());

    private static string MetricTitle(MetricsBarItem item) => item.Kind switch
    {
        MetricsBarItemKind.CPU => Strings.CPU,
        MetricsBarItemKind.Memory => Strings.Memory,
        MetricsBarItemKind.Storage => Strings.Storage,
        MetricsBarItemKind.Battery => Strings.Battery,
        MetricsBarItemKind.Network => Strings.Network,
        _ => item.Title ?? Strings.CustomMetrics,
    };

    private void OnCustomMetricsChanged(IReadOnlyList<CustomMetricsBundle> bundles) => uiContext.Post(_ =>
    {
        if (dashboard.IsDisposed)
        {
            return;
        }
        RenderDashboard();
        RenderMetricsBar();
        settingsForm?.RefreshCustomMetricsSources();
    }, null);

    private void OnMetricsUpdated(Metrics metrics)
    {
        var cpuInfo = metrics.SystemInfoBundle.CPUInfo;
        UpdateAnimationInterval(cpuInfo);
        UpdateToolTip();
        RenderDashboard();
        RenderMetricsBar();
    }

    private void OnAIUsageUpdated() => uiContext.Post(_ =>
    {
        if (dashboard.IsDisposed)
        {
            return;
        }
        RenderDashboard();
        UpdateToolTip();
    }, null);

    private void UpdateToolTip()
    {
        var lines = new List<string> { AppName };
        if (metricsMonitor.Metrics.SystemInfoBundle.CPUInfo is { } cpuInfo)
        {
            lines.Add($"{Strings.CPU}: {MetricsFormatter.FormatPercentage(cpuInfo.Percentage).Trim()}");
        }
        var rows = UsageDashboardRows.Build(aiUsageMonitor.Snapshot.Bundles ?? [], DateTimeOffset.Now,
            UsageStrings.For(CultureInfo.CurrentUICulture), CultureInfo.CurrentUICulture);
        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.Compact))
            {
                lines.Add($"{row.Title}: {row.Compact}");
            }
        }
        runnerTrayIcon.SetToolTip(TrayToolTip.Compose(lines, RunnerTrayIcon.MaximumToolTipLength));
    }

    private static double NowSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void UpdateAnimationInterval(CPUInfo? cpuInfo)
    {
        duckPlayer?.UpdateLoad(cpuInfo?.Percentage.Value, settings.SpeedDecreasesUnderLoad, NowSeconds);
        animationTimer.Interval = duckPlayer is null
            ? RunnerSpeed.TimerIntervalMilliseconds(cpuInfo, settings.SpeedDecreasesUnderLoad)
            : 16;
    }

    private void Animate()
    {
        var now = NowSeconds;
        if (duckPlayer is null) runnerTrayIcon.AdvanceFrame();
        else
        {
            duckPlayer.Advance(Math.Clamp(now - lastAnimationTime, 0, 0.25));
            runnerTrayIcon.SelectFrame(duckPlayer.FrameIndex);
        }
        lastAnimationTime = now;
    }

    private void ToggleDashboard()
    {
        if (dashboard.Visible)
        {
            dashboard.Hide();
            return;
        }
        if (DateTimeOffset.Now - dashboard.LastHiddenAt < TimeSpan.FromMilliseconds(300))
        {
            return;
        }
        RenderDashboard();
        dashboard.ShowNear(Cursor.Position);
    }

    private void OpenLicenses()
    {
        if (licensesForm is null || licensesForm.IsDisposed)
        {
            licensesForm = new LicensesForm(AppName);
        }
        licensesForm.Show();
        licensesForm.Activate();
    }

    private static void OpenTaskManager() => Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });

    private static void OpenURL(string url) => Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Write($"Starting {startInfo.FileName} failed: {exception.Message}");
        }
    }

    private static int LogicalSize(int value)
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return (int)Math.Round(value * graphics.DpiX / 96d);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs arguments)
    {
        if (arguments.Category == UserPreferenceCategory.General)
        {
            appMenu.InvalidateRunnerThumbnails();
            RenderIcons();
            RenderMetricsBar();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs arguments)
    {
        appMenu.InvalidateRunnerThumbnails();
        RenderIcons();
        RenderMetricsBar();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs arguments)
    {
        switch (arguments.Mode)
        {
            case PowerModes.Suspend:
                aiUsageTimer.Stop();
                StopMonitoring();
                break;
            case PowerModes.Resume:
                StartMonitoring();
                _ = Task.Run(aiUsageMonitor.RefreshAsync);
                aiUsageTimer.Start();
                break;
        }
    }

    private void DisposeSourceFrames()
    {
        foreach (var frame in sourceFrames.Distinct())
        {
            frame.Dispose();
        }
        sourceFrames = [];
    }
}
