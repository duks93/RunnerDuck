/*
 CPUUsage.cs
 RunnerDuck.Core

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

namespace RunnerDuck.Core;

public readonly record struct SystemTimes(long Idle, long Kernel, long User);

public static class CPUUsage
{
    public static CPUInfo Calculate(SystemTimes previous, SystemTimes current)
    {
        var idle = current.Idle - previous.Idle;
        var kernel = current.Kernel - previous.Kernel;
        var user = current.User - previous.User;
        var total = (double)kernel + user;
        if (total <= 0 || idle < 0 || kernel < 0 || user < 0)
        {
            return new CPUInfo(Idle: new Percentage(1));
        }
        return new CPUInfo(
            Percentage: Ratio(total - idle, total),
            System: Ratio(kernel - idle, total),
            User: Ratio(user, total),
            Idle: Ratio(idle, total)
        );
    }

    private static Percentage Ratio(double part, double total) => new(Math.Clamp(part / total, 0, 1));
}
