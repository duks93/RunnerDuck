/*
 AIUsageWindow.cs
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

public enum AIUsageWindowKindType
{
    FiveHour,
    Weekly,
    Monthly,
    WeeklyModel,
}

public readonly record struct AIUsageWindowKind(AIUsageWindowKindType Type, string? Model = null)
{
    public static AIUsageWindowKind FiveHour { get; } = new(AIUsageWindowKindType.FiveHour);
    public static AIUsageWindowKind Weekly { get; } = new(AIUsageWindowKindType.Weekly);
    public static AIUsageWindowKind Monthly { get; } = new(AIUsageWindowKindType.Monthly);

    public static AIUsageWindowKind WeeklyModel(string model) => new(AIUsageWindowKindType.WeeklyModel, model);
}

public sealed record AIUsageWindow(AIUsageWindowKind Kind, double Percentage, DateTimeOffset? ResetDate = null)
{
    [JsonIgnore]
    public double RemainingPercentage => 100 - Percentage;
}
