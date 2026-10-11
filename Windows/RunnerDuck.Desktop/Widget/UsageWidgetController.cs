/*
 UsageWidgetController.cs
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

using System.Globalization;
using Microsoft.Windows.Widgets.Providers;
using RunnerDuck.AIUsage;
using RunnerDuck.AIUsage.Presentation;
using RunnerDuck.Core;

namespace RunnerDuck.App.Widget;

internal sealed class UsageWidgetController
{
    private const string RefreshVerb = "refresh";

    private static readonly TimeSpan ActivationRefreshThreshold = TimeSpan.FromMinutes(1);

    private readonly AIUsageMonitor monitor;
    private readonly Lazy<string> template = new(LoadTemplate);
    private readonly ManualResetEvent allWidgetsRemoved = new(false);
    private readonly Lock gate = new();
    private readonly Lock pushGate = new();
    private readonly Dictionary<string, bool> activeStates = [];
    private readonly System.Threading.Timer timer;
    private bool isTimerRunning;
    private bool hasRecoveredWidgets;

    private UsageWidgetController()
    {
        var service = new AIUsageService(AIUsageEnvironment.Live(Log.Write));
        monitor = new AIUsageMonitor(AppPaths.SettingsFile, service.RefreshAsync, Log.Write);
        monitor.Updated += PushAll;
        timer = new System.Threading.Timer(_ => StartRefresh());
    }

    public static UsageWidgetController Shared { get; } = new();

    public void RecoverWidgets()
    {
        lock (gate)
        {
            if (hasRecoveredWidgets)
            {
                return;
            }
            hasRecoveredWidgets = true;
        }
        try
        {
            var widgetInfos = WidgetManager.GetDefault().GetWidgetInfos() ?? [];
            var widgetIDs = widgetInfos.Select(info => info.WidgetContext.Id).ToList();
            lock (gate)
            {
                widgetIDs.ForEach(widgetID => activeStates.TryAdd(widgetID, false));
            }
            Log.Write($"Recovered {widgetIDs.Count} widget(s).");
        }
        catch (Exception exception)
        {
            Log.Write($"Recovering widgets failed: {exception.Message}");
        }
    }

    public void Create(string widgetID)
    {
        lock (gate)
        {
            activeStates[widgetID] = false;
        }
        Log.Write($"Widget created: {widgetID}");
        Push(widgetID);
        RefreshIfStale();
    }

    public void Delete(string widgetID)
    {
        bool isEmpty;
        lock (gate)
        {
            activeStates.Remove(widgetID);
            isEmpty = activeStates.Count == 0;
        }
        Log.Write($"Widget deleted: {widgetID}");
        UpdateTimer();
        if (isEmpty)
        {
            timer.Dispose();
            monitor.Dispose();
            allWidgetsRemoved.Set();
        }
    }

    public void Activate(string widgetID)
    {
        lock (gate)
        {
            activeStates[widgetID] = true;
        }
        UpdateTimer();
        Push(widgetID);
        RefreshIfStale();
    }

    public void Deactivate(string widgetID)
    {
        lock (gate)
        {
            if (activeStates.ContainsKey(widgetID))
            {
                activeStates[widgetID] = false;
            }
        }
        UpdateTimer();
    }

    public void InvokeAction(string widgetID, string verb)
    {
        if (verb == RefreshVerb)
        {
            StartRefresh();
        }
    }

    public void Push(string widgetID)
    {
        lock (pushGate) { Update(widgetID, CurrentData()); }
    }

    public void WaitUntilAllWidgetsRemoved() => allWidgetsRemoved.WaitOne();

    private static string LoadTemplate()
    {
        using var stream = typeof(UsageWidgetController).Assembly.GetManifestResourceStream("UsageWidget.json")
            ?? throw new InvalidOperationException("The widget template is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void RefreshIfStale()
    {
        if (monitor.Snapshot.LastRefreshDate is { } date && DateTimeOffset.Now - date < ActivationRefreshThreshold)
        {
            return;
        }
        StartRefresh();
    }

    private void StartRefresh() => _ = Task.Run(monitor.RefreshAsync);

    private void UpdateTimer()
    {
        lock (gate)
        {
            var hasActiveWidget = activeStates.ContainsValue(true);
            if (hasActiveWidget == isTimerRunning)
            {
                return;
            }
            isTimerRunning = hasActiveWidget;
            var interval = hasActiveWidget ? AIUsageService.RefreshInterval : Timeout.InfiniteTimeSpan;
            timer.Change(interval, interval);
        }
    }

    private void PushAll()
    {
        lock (pushGate)
        {
            List<string> widgetIDs;
            lock (gate) { widgetIDs = activeStates.Keys.ToList(); }
            var data = CurrentData();
            widgetIDs.ForEach(widgetID => Update(widgetID, data));
        }
    }

    private string CurrentData()
    {
        var snapshot = monitor.Snapshot;
        var culture = CultureInfo.CurrentUICulture;
        return UsageCardBuilder
            .Build(snapshot.Bundles, snapshot.LastRefreshDate, DateTimeOffset.Now, UsageStrings.For(culture), culture)
            .ToJsonString();
    }

    private void Update(string widgetID, string data)
    {
        try
        {
            WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(widgetID)
            {
                Template = template.Value,
                Data = data,
            });
        }
        catch (Exception exception)
        {
            Log.Write($"Updating widget {widgetID} failed: {exception.Message}");
        }
    }
}
