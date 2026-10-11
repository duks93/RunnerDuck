/*
 UsageStrings.cs
 RunnerDuck.AIUsage

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

namespace RunnerDuck.AIUsage.Presentation;

public sealed class UsageStrings
{
    public required string FiveHourShort { get; init; }
    public required string WeeklyShort { get; init; }
    public required string MonthlyShort { get; init; }
    public required string SignInExpired { get; init; }
    public required string Failed { get; init; }
    public required string Loading { get; init; }
    public required string Empty { get; init; }
    public required string LastUpdatedFormat { get; init; }
    public required string Refresh { get; init; }
    public required string DaysHoursFormat { get; init; }
    public required string HoursMinutesFormat { get; init; }
    public required string MinutesFormat { get; init; }

    public static UsageStrings English { get; } = new()
    {
        FiveHourShort = "5h",
        WeeklyShort = "Wk",
        MonthlyShort = "Mo",
        SignInExpired = "Sign-in expired",
        Failed = "Failed",
        Loading = "Loading…",
        Empty = "No signed-in AI coding tools found.",
        LastUpdatedFormat = "Last updated: {0}",
        Refresh = "Refresh",
        DaysHoursFormat = "{0}d {1}h",
        HoursMinutesFormat = "{0}h {1}m",
        MinutesFormat = "{0}m",
    };

    public static UsageStrings Korean { get; } = new()
    {
        FiveHourShort = "5시간",
        WeeklyShort = "주간",
        MonthlyShort = "월간",
        SignInExpired = "로그인 만료",
        Failed = "실패",
        Loading = "불러오는 중…",
        Empty = "로그인된 AI 코딩 도구가 없습니다.",
        LastUpdatedFormat = "마지막 업데이트: {0}",
        Refresh = "새로고침",
        DaysHoursFormat = "{0}일 {1}시간",
        HoursMinutesFormat = "{0}시간 {1}분",
        MinutesFormat = "{0}분",
    };

    public static UsageStrings For(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "ko" ? Korean : English;
}
