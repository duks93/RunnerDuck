/*
 CustomMetricsSnapshot.cs
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunnerDuck.Core;

public sealed record CustomMetricsSnapshot
{
    private static readonly JsonSerializerOptions options = new() { RespectNullableAnnotations = true };

    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("metricsBarValue")] public string? MetricsBarValue { get; init; }
    [JsonPropertyName("metrics")] public required IReadOnlyList<CustomMetric> Metrics { get; init; }
    [JsonPropertyName("lastUpdatedDate")] public required DateTimeOffset LastUpdatedDate { get; init; }

    public static CustomMetricsSnapshot Decode(byte[] data)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<CustomMetricsSnapshot>(data, options);
            if (snapshot is null || snapshot.Metrics.Any(metric => metric is null))
            {
                throw new CustomMetricsException(CustomMetricsError.InvalidFormat);
            }
            return snapshot;
        }
        catch (JsonException)
        {
            throw new CustomMetricsException(CustomMetricsError.InvalidFormat);
        }
    }

    public bool Equals(CustomMetricsSnapshot? other) =>
        other is not null
        && Title == other.Title
        && Symbol == other.Symbol
        && MetricsBarValue == other.MetricsBarValue
        && Metrics.SequenceEqual(other.Metrics)
        && LastUpdatedDate == other.LastUpdatedDate;

    public override int GetHashCode() => HashCode.Combine(Title, Symbol, MetricsBarValue, Metrics.Count, LastUpdatedDate);
}

public sealed record CustomMetric
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("formattedValue")] public required string FormattedValue { get; init; }
    [JsonPropertyName("normalizedValue")] public double? NormalizedValue { get; init; }

    public double? ClampedNormalizedValue =>
        NormalizedValue is { } value && double.IsFinite(value) ? Math.Clamp(value, 0, 1) : null;
}

public sealed record CustomMetricsBundle(Guid ID, CustomMetricsSnapshot Snapshot, bool IsFailed = false)
{
    public const string MissingValueLabel = "---";

    public string MetricsBarLabel => IsFailed ? MissingValueLabel : Snapshot.MetricsBarValue ?? MissingValueLabel;
}

public enum CustomMetricsError
{
    FileUnreadable,
    InvalidFormat,
}

public sealed class CustomMetricsException(CustomMetricsError error) : Exception(error.ToString())
{
    public CustomMetricsError Error { get; } = error;
}
