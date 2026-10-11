/*
 RelativeTime.cs
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

public enum RelativeTimeUnit
{
    Now,
    Seconds,
    Minutes,
    Hours,
    Days,
}

public readonly record struct RelativeTime(RelativeTimeUnit Unit, int Amount, bool IsFuture)
{
    public static RelativeTime From(DateTimeOffset date, DateTimeOffset now)
    {
        var interval = now - date;
        var isFuture = interval < TimeSpan.Zero;
        var magnitude = interval.Duration();
        return magnitude switch
        {
            { TotalSeconds: < 1 } => new(RelativeTimeUnit.Now, 0, false),
            { TotalMinutes: < 1 } => new(RelativeTimeUnit.Seconds, (int)magnitude.TotalSeconds, isFuture),
            { TotalHours: < 1 } => new(RelativeTimeUnit.Minutes, (int)magnitude.TotalMinutes, isFuture),
            { TotalDays: < 1 } => new(RelativeTimeUnit.Hours, (int)magnitude.TotalHours, isFuture),
            _ => new(RelativeTimeUnit.Days, (int)magnitude.TotalDays, isFuture),
        };
    }
}
