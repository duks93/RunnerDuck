/*
 CustomMetricsSettingsControl.cs
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
using RunnerDuck.App.Properties;
using RunnerDuck.Core;

namespace RunnerDuck.App.Settings;

internal sealed class CustomMetricsSettingsControl : TableLayoutPanel
{
    private readonly ISettingsHost host;
    private readonly ListView sources = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        ShowItemToolTips = true,
        Dock = DockStyle.Fill,
        AccessibleName = Strings.CustomMetrics,
    };
    private readonly Button removeButton;
    private readonly Button moveUpButton;
    private readonly Button moveDownButton;
    private readonly Button showFileButton;

    public CustomMetricsSettingsControl(ISettingsHost host)
    {
        this.host = host;
        AutoSize = true;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        ColumnCount = 1;
        RowCount = 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sources.Height = LogicalToDeviceUnits(150);
        sources.Columns.Add(Strings.CustomMetricsSourceName, LogicalToDeviceUnits(145));
        sources.Columns.Add(Strings.CustomMetricsSourceStatus, LogicalToDeviceUnits(115));
        sources.Columns.Add(Strings.CustomMetricsFilePath, LogicalToDeviceUnits(250));
        sources.SelectedIndexChanged += (_, _) => UpdateButtons();
        sources.ItemActivate += (_, _) => ShowFile();
        Controls.Add(sources, 0, 0);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        removeButton = Button(Strings.Remove, RemoveSource);
        moveUpButton = Button(Strings.MoveUp, () => MoveSource(-1));
        moveDownButton = Button(Strings.MoveDown, () => MoveSource(1));
        showFileButton = Button(Strings.ShowInExplorer, ShowFile);
        buttons.Controls.AddRange([Button(Strings.Add, AddSource), removeButton, moveUpButton, moveDownButton, showFileButton]);
        Controls.Add(buttons, 0, 1);
        UpdateButtons();
    }

    public void RefreshSources(Guid? selectedID = null)
    {
        if (IsDisposed)
        {
            return;
        }
        selectedID ??= SelectedSource?.ID;
        var latest = host.CustomMetricsSources();
        sources.BeginUpdate();
        try
        {
            for (var index = 0; index < latest.Count; index++)
            {
                var source = latest[index];
                var failed = host.IsCustomMetricsSourceFailed(source.ID);
                if (index >= sources.Items.Count)
                {
                    sources.Items.Add(new ListViewItem(["", "", ""]));
                }
                var item = sources.Items[index];
                item.Tag = source;
                item.Text = source.DisplayName;
                item.SubItems[1].Text = failed ? Strings.ErrorDetected : "";
                item.SubItems[2].Text = source.FilePath;
                item.ToolTipText = source.FilePath;
                item.Selected = source.ID == selectedID;
            }
            while (sources.Items.Count > latest.Count)
            {
                sources.Items.RemoveAt(sources.Items.Count - 1);
            }
        }
        finally
        {
            sources.EndUpdate();
        }
        UpdateButtons();
    }

    private CustomMetricsSource? SelectedSource => sources.SelectedItems.Count == 1
        ? sources.SelectedItems[0].Tag as CustomMetricsSource
        : null;

    private void UpdateButtons()
    {
        var index = sources.SelectedIndices.Count == 1 ? sources.SelectedIndices[0] : -1;
        removeButton.Enabled = showFileButton.Enabled = index >= 0;
        moveUpButton.Enabled = index > 0;
        moveDownButton.Enabled = index >= 0 && index < sources.Items.Count - 1;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private void AddSource()
    {
        using var dialog = new OpenFileDialog
        {
            Title = Strings.AddCustomMetricsSource,
            Filter = Strings.JSONFileFilter,
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        TryChange(() => host.AddCustomMetricsSource(dialog.FileName), () => host.CustomMetricsSources().LastOrDefault()?.ID);
    }

    private void RemoveSource()
    {
        if (SelectedSource is not { } source)
        {
            return;
        }
        if (MessageBox.Show(this,
            $"{source.DisplayName}\n\n{Strings.CustomMetricsConfirmationMessage}",
            Strings.RemoveCustomMetrics, MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }
        var index = sources.SelectedIndices[0];
        TryChange(() => host.RemoveCustomMetricsSource(source.ID), () =>
        {
            var remaining = host.CustomMetricsSources();
            return remaining.Count == 0 ? null : remaining[Math.Min(index, remaining.Count - 1)].ID;
        });
    }

    private void MoveSource(int offset)
    {
        if (SelectedSource is not { } source)
        {
            return;
        }
        var index = sources.SelectedIndices[0];
        if (index + offset < 0 || index + offset >= sources.Items.Count)
        {
            return;
        }
        TryChange(() => host.MoveCustomMetricsSource(index, index + offset), () => source.ID);
    }

    private void TryChange(Action change, Func<Guid?> selection)
    {
        try
        {
            change();
            RefreshSources(selection());
            if (sources.SelectedItems.Count == 1)
            {
                sources.SelectedItems[0].EnsureVisible();
            }
        }
        catch (CustomMetricsException exception)
        {
            ShowError(exception.Error == CustomMetricsError.FileUnreadable ? Strings.FileUnreadable : Strings.InvalidFormat);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Saving custom metrics settings failed: {exception.Message}");
            ShowError(Strings.SettingsSavingFailed);
            RefreshSources();
        }
    }

    private void ShowFile()
    {
        if (SelectedSource is not { } source)
        {
            return;
        }
        try
        {
            if (!File.Exists(source.FilePath))
            {
                ShowError(Strings.FileUnreadable);
                return;
            }
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("/select,");
            startInfo.ArgumentList.Add(Path.GetFullPath(source.FilePath));
            Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Write($"Showing custom metrics file failed: {exception.Message}");
            ShowError(Strings.FileUnreadable);
        }
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, Strings.CustomMetrics, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
