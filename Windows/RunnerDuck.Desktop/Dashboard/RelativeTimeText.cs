/*
 RelativeTimeText.cs
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
using RunnerDuck.Core;

namespace RunnerDuck.App.Dashboard;

internal static class RelativeTimeText
{
    public static string Format(DateTimeOffset date, DateTimeOffset now)
    {
        var relativeTime = RelativeTime.From(date, now);
        if (relativeTime.Unit == RelativeTimeUnit.Days && relativeTime.Amount == 1)
        {
            return relativeTime.IsFuture ? Strings.Tomorrow : Strings.Yesterday;
        }
        var format = (relativeTime.Unit, relativeTime.IsFuture) switch
        {
            (RelativeTimeUnit.Now, _) => Strings.RelativeNow,
            (RelativeTimeUnit.Seconds, false) => Strings.SecondsAgo,
            (RelativeTimeUnit.Minutes, false) => Strings.MinutesAgo,
            (RelativeTimeUnit.Hours, false) => Strings.HoursAgo,
            (RelativeTimeUnit.Days, false) => Strings.DaysAgo,
            (RelativeTimeUnit.Seconds, true) => Strings.InSeconds,
            (RelativeTimeUnit.Minutes, true) => Strings.InMinutes,
            (RelativeTimeUnit.Hours, true) => Strings.InHours,
            _ => Strings.InDays,
        };
        return string.Format(format, relativeTime.Amount);
    }
}
