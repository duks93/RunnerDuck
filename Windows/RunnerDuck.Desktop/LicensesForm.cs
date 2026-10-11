/*
 LicensesForm.cs
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

namespace RunnerDuck.App;

internal sealed class LicensesForm : Form
{
    private static string LicenseText => string.Join("\r\n\r\n", new[] { "NOTICE.txt", "LICENSE.txt", "THIRD_PARTY.txt" }
        .Select(name => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Legal", name))));

    public LicensesForm(string appName)
    {
        Text = $"{Strings.OpenSourceLicense} - {appName}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(420));
        MinimizeBox = false;
        MaximizeBox = false;
        var description = new Label
        {
            Text = Strings.OpenSourceLicenseDescription,
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(32),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(LogicalToDeviceUnits(8), 0, 0, 0),
        };
        var licenses = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = LicenseText.ReplaceLineEndings("\r\n"),
            Font = new Font("Segoe UI", 9.75f),
        };
        Controls.Add(licenses);
        Controls.Add(description);
    }
}
