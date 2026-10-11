/*
 SettingsPage.cs
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

namespace RunnerDuck.App.Settings;

internal sealed class SettingsPage : TableLayoutPanel
{
    private static readonly Font SectionFont = new("Segoe UI", 10.5f, FontStyle.Bold);

    private readonly List<Label> wrappingLabels = [];

    public SettingsPage()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        ColumnCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        Padding = new Padding(LogicalToDeviceUnits(16), LogicalToDeviceUnits(8), LogicalToDeviceUnits(16), LogicalToDeviceUnits(16));
        Layout += (_, _) => UpdateWrappingWidths();
    }

    public Label AddSection(string title)
    {
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Font = SectionFont,
            Margin = new Padding(0, LogicalToDeviceUnits(16), 0, LogicalToDeviceUnits(6)),
        };
        return Add(label);
    }

    public Label AddNote(string text = "")
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(LogicalToDeviceUnits(2), LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(6)),
        };
        wrappingLabels.Add(label);
        return Add(label);
    }

    public CheckBox AddCheckBox(string text, bool isChecked, Action<bool> changed)
    {
        var checkBox = new CheckBox { Text = text, Checked = isChecked, AutoSize = true, Margin = new Padding(0, LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(2)) };
        checkBox.CheckedChanged += (_, _) => changed(checkBox.Checked);
        return Add(checkBox);
    }

    public T Add<T>(T control) where T : Control
    {
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(control, 0, RowCount);
        RowCount++;
        return control;
    }

    private void UpdateWrappingWidths()
    {
        var width = Math.Max(LogicalToDeviceUnits(100), ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (var label in wrappingLabels)
        {
            label.MaximumSize = new Size(width, 0);
        }
    }
}
