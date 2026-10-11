/*
 DashboardTheme.cs
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

using RunnerDuck.App.Tray;

namespace RunnerDuck.App.Dashboard;

internal sealed record DashboardTheme(
    bool IsLight,
    Color Background,
    Color CardBackground,
    Color Text,
    Color SecondaryText,
    Color Divider,
    Color Accent,
    Color Track,
    Color Failure,
    Color Warning
)
{
    public static DashboardTheme Current()
    {
        var accent = WindowsTheme.AccentColor();
        return WindowsTheme.AppsUseLightTheme()
            ? new(
                true,
                Color.FromArgb(243, 243, 243),
                Color.White,
                Color.FromArgb(27, 27, 27),
                Color.FromArgb(96, 96, 96),
                Color.FromArgb(229, 229, 229),
                accent,
                Color.FromArgb(220, 220, 220),
                Color.FromArgb(196, 43, 28),
                Color.FromArgb(157, 93, 0)
            )
            : new(
                false,
                Color.FromArgb(32, 32, 32),
                Color.FromArgb(43, 43, 43),
                Color.White,
                Color.FromArgb(200, 200, 200),
                Color.FromArgb(61, 61, 61),
                accent,
                Color.FromArgb(70, 70, 70),
                Color.FromArgb(255, 153, 164),
                Color.FromArgb(252, 225, 0)
            );
    }
}
