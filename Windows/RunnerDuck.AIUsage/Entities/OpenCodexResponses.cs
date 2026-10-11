/*
 OpenCodexResponses.cs
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

using System.Text.Json.Serialization;

namespace RunnerDuck.AIUsage;

public sealed class OpenCodexRuntimePort
{
    [JsonPropertyName("port")] public required int Port { get; init; }
}

public sealed class OpenCodexAccountsResponse
{
    [JsonPropertyName("accounts")] public IReadOnlyList<Account>? Accounts { get; init; }

    public sealed class Account
    {
        [JsonPropertyName("id")] public required string ID { get; init; }
        [JsonPropertyName("alias")] public string? Alias { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("isMain")] public bool? IsMain { get; init; }
        [JsonPropertyName("needsReauth")] public bool? NeedsReauth { get; init; }
        [JsonPropertyName("quotaUnavailable")] public bool? QuotaUnavailable { get; init; }
        [JsonPropertyName("quota")] public Quota? AccountQuota { get; init; }
    }

    public sealed class Quota
    {
        [JsonPropertyName("fiveHourPercent")] public FlexibleNumber? FiveHourPercent { get; init; }
        [JsonPropertyName("fiveHourResetAt")] public FlexibleNumber? FiveHourResetAt { get; init; }
        [JsonPropertyName("shortPercent")] public FlexibleNumber? ShortPercent { get; init; }
        [JsonPropertyName("shortResetAt")] public FlexibleNumber? ShortResetAt { get; init; }
        [JsonPropertyName("weeklyPercent")] public FlexibleNumber? WeeklyPercent { get; init; }
        [JsonPropertyName("weeklyResetAt")] public FlexibleNumber? WeeklyResetAt { get; init; }
        [JsonPropertyName("monthlyPercent")] public FlexibleNumber? MonthlyPercent { get; init; }
        [JsonPropertyName("monthlyResetAt")] public FlexibleNumber? MonthlyResetAt { get; init; }
        [JsonPropertyName("customWindows")] public IReadOnlyList<CustomWindow>? CustomWindows { get; init; }
    }

    public sealed class CustomWindow
    {
        [JsonPropertyName("label")] public required string Label { get; init; }
        [JsonPropertyName("percent")] public FlexibleNumber? Percent { get; init; }
        [JsonPropertyName("resetAt")] public FlexibleNumber? ResetAt { get; init; }
    }
}
