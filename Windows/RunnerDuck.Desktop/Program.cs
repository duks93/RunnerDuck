/*
 Program.cs
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

using Microsoft.Win32;
using RunnerDuck.App.Properties;
using RunnerDuck.App.Tray;
using RunnerDuck.App.Widget;
using RunnerDuck.App.Widget.Com;

namespace RunnerDuck.App;

public static class Program
{
    private const string ComServerArgument = "-RegisterProcessAsComServer";
    private const string SingleInstanceMutexName = @"Local\RunnerDuck.Tray";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--verify-animation")
        {
            AnimationVerification.Render(args[1]);
            return;
        }
        var components = InstalledComponents.Load();
        if (args.Contains(ComServerArgument))
        {
            if (components.Widget) RunWidgetProvider();
            return;
        }
        if (!components.Tray)
        {
            MessageBox.Show(
                "RunnerDuck: Widget only / 위젯 전용\n\nWin+W → Widget gallery / 위젯 갤러리 → RunnerDuck / AI Usage",
                "RunnerDuck", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var defaultTrayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RunnerDuck", "TrayApp", "RunnerDuck.exe");
        if (!TrayLaunchGuard.RunIfAllowed(Environment.ProcessPath,
            TrayLaunchGuard.ReadInstalledExecutable(Registry.CurrentUser), TrayLaunchGuard.HasPackageIdentity(),
            defaultTrayPath, RunTrayApplication))
        {
            MessageBox.Show(Strings.TrayInstallationRequired, "RunnerDuck", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void RunTrayApplication()
    {
        using var mutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }
        Log.Configure("App.log");
        Log.Write("Tray application started.");
        Settings.LaunchAtLogin.EnableByDefault();
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        Application.Run(new RunnerDuckApplicationContext());
        Log.Write("Tray application exited.");
    }

    private static void RunWidgetProvider()
    {
        Log.Configure("Widget.log");
        var thread = new Thread(() =>
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            using var registration = ClassObjectRegistration.Register<UsageWidgetProvider>();
            Log.Write("Widget provider registered.");
            UsageWidgetController.Shared.WaitUntilAllWidgetsRemoved();
            Log.Write("All widgets removed. Exiting.");
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        thread.Join();
    }
}
