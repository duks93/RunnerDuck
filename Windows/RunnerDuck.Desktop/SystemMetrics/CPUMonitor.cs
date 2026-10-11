/*
 CPUMonitor.cs
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

using System.Runtime.InteropServices;
using RunnerDuck.Core;

namespace RunnerDuck.App.SystemMetrics;

internal sealed partial class CPUMonitor
{
    private readonly Lock gate = new();
    private SystemTimes? previous;

    public CPUInfo? Sample()
    {
        lock (gate)
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return null;
            }
            var current = new SystemTimes(idle, kernel, user);
            var last = previous;
            previous = current;
            return last is null ? null : CPUUsage.Calculate(last.Value, current);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
}
