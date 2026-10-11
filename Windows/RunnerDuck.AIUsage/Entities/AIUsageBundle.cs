/*
 AIUsageBundle.cs
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

public enum AIUsageStatus
{
    Available,
    SignInRequired,
    Failed,
}

public sealed record AIUsageBundle(
    AIUsageProvider Provider,
    AIUsageStatus Status,
    IReadOnlyList<AIUsageWindow> Windows,
    DateTimeOffset LastUpdatedDate,
    string? AccountID = null,
    string? AccountName = null
)
{
    [JsonIgnore]
    public string ID => $"{Provider.RawValue()}/{AccountID ?? "local"}";

    [JsonIgnore]
    public string DisplayName => AccountName is null
        ? Provider.DisplayName()
        : $"{Provider.DisplayName()} · {AccountName}";

    [JsonIgnore]
    public IReadOnlyList<AIUsageWindow> SummaryWindows
    {
        get
        {
            if (Status != AIUsageStatus.Available)
            {
                return [];
            }
            var fiveHour = Windows.FirstOrDefault(window => window.Kind == AIUsageWindowKind.FiveHour);
            var longest = Windows.FirstOrDefault(window => window.Kind == AIUsageWindowKind.Weekly)
                ?? Windows.FirstOrDefault(window => window.Kind == AIUsageWindowKind.Monthly);
            return new[] { fiveHour, longest }.OfType<AIUsageWindow>().ToList();
        }
    }

    public bool Equals(AIUsageBundle? other) =>
        other is not null
        && Provider == other.Provider
        && Status == other.Status
        && Windows.SequenceEqual(other.Windows)
        && LastUpdatedDate == other.LastUpdatedDate
        && AccountID == other.AccountID
        && AccountName == other.AccountName;

    public override int GetHashCode() => HashCode.Combine(Provider, Status, Windows.Count, LastUpdatedDate, AccountID, AccountName);
}
