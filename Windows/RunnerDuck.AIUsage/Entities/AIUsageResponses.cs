/*
 AIUsageResponses.cs
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

public sealed class ClaudeCodeCredentials
{
    [JsonPropertyName("claudeAiOauth")] public required OAuthCredentials ClaudeAiOauth { get; init; }

    public sealed class OAuthCredentials
    {
        [JsonPropertyName("accessToken")] public required string AccessToken { get; init; }
        [JsonPropertyName("expiresAt")] public double? ExpiresAt { get; init; }
    }
}

public sealed class ClaudeCodeUsageResponse
{
    [JsonPropertyName("five_hour")] public Bucket? FiveHour { get; init; }
    [JsonPropertyName("seven_day")] public Bucket? SevenDay { get; init; }
    [JsonPropertyName("seven_day_fable")] public Bucket? SevenDayFable { get; init; }
    [JsonPropertyName("seven_day_opus")] public Bucket? SevenDayOpus { get; init; }
    [JsonPropertyName("seven_day_sonnet")] public Bucket? SevenDaySonnet { get; init; }
    [JsonPropertyName("limits")] public IReadOnlyList<Limit>? Limits { get; init; }

    public sealed class Bucket
    {
        [JsonPropertyName("utilization")] public FlexibleNumber? Utilization { get; init; }
        [JsonPropertyName("resets_at")] public string? ResetsAt { get; init; }
    }

    public sealed class Limit
    {
        [JsonPropertyName("kind")] public string? Kind { get; init; }
        [JsonPropertyName("percent")] public FlexibleNumber? Percent { get; init; }
        [JsonPropertyName("resets_at")] public string? ResetsAt { get; init; }
        [JsonPropertyName("scope")] public LimitScope? Scope { get; init; }
    }

    public sealed class LimitScope
    {
        [JsonPropertyName("model")] public LimitModel? Model { get; init; }
    }

    public sealed class LimitModel
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; init; }
    }
}

public sealed class CodexAuth
{
    [JsonPropertyName("tokens")] public Tokens? AuthTokens { get; init; }

    public sealed class Tokens
    {
        [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
        [JsonPropertyName("account_id")] public string? AccountID { get; init; }
    }
}

public sealed class CodexUsageResponse
{
    [JsonPropertyName("rate_limit")] public RateLimitInfo? RateLimit { get; init; }

    public sealed class RateLimitInfo
    {
        [JsonPropertyName("primary_window")] public Window? PrimaryWindow { get; init; }
        [JsonPropertyName("secondary_window")] public Window? SecondaryWindow { get; init; }
        [JsonPropertyName("tertiary_window")] public Window? TertiaryWindow { get; init; }
    }

    public sealed class Window
    {
        [JsonPropertyName("used_percent")] public FlexibleNumber? UsedPercent { get; init; }
        [JsonPropertyName("reset_at")] public FlexibleNumber? ResetAt { get; init; }
        [JsonPropertyName("limit_window_seconds")] public FlexibleNumber? LimitWindowSeconds { get; init; }
    }
}

public sealed class GrokAuthEntry
{
    [JsonPropertyName("key")] public required string Key { get; init; }
    [JsonPropertyName("user_id")] public string? UserID { get; init; }
}

public sealed class GrokBillingResponse
{
    [JsonPropertyName("config")] public BillingConfig? Config { get; init; }

    public sealed class BillingConfig
    {
        [JsonPropertyName("creditUsagePercent")] public FlexibleNumber? CreditUsagePercent { get; init; }
        [JsonPropertyName("currentPeriod")] public Period? CurrentPeriod { get; init; }
        [JsonPropertyName("monthlyLimit")] public Cents? MonthlyLimit { get; init; }
        [JsonPropertyName("used")] public Cents? Used { get; init; }
        [JsonPropertyName("billingPeriodEnd")] public FlexibleDate? BillingPeriodEnd { get; init; }
    }

    public sealed class Period
    {
        [JsonPropertyName("type")] public string? Type { get; init; }
        [JsonPropertyName("end")] public FlexibleDate? End { get; init; }
    }

    public sealed class Cents
    {
        [JsonPropertyName("val")] public FlexibleNumber? Val { get; init; }
    }
}
