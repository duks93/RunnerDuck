/*
 Runner.cs
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

public abstract record RunnerSource
{
    public sealed record BuiltIn(RunnerKind Kind) : RunnerSource;
    public sealed record Custom(string Name) : RunnerSource;
}

[JsonConverter(typeof(RunnerJSONConverter))]
public sealed record Runner
{
    public string ID { get; }
    public bool IsTemplate { get; }
    public FrameOrder FrameOrder { get; }
    public RunnerSource Source { get; }
    public bool IsCustom => Source is RunnerSource.Custom;

    public Runner(string id, string name, bool isTemplate, FrameOrder frameOrder)
    {
        ID = id;
        IsTemplate = isTemplate;
        FrameOrder = frameOrder;
        Source = new RunnerSource.Custom(name);
    }

    public Runner(RunnerKind kind)
    {
        ID = kind.ID();
        IsTemplate = kind.IsTemplate();
        FrameOrder = kind.FrameOrder();
        Source = new RunnerSource.BuiltIn(kind);
    }

    public IReadOnlyList<string> ResourceNames() => FrameOrder.Order
        .Select(number => IsCustom ? $"frame-{number}" : $"{ID}-frame-{number}")
        .ToArray();

    public static Runner Default { get; } = new(RunnerKind.Duck);

    public bool Equals(Runner? other) => other is not null && ID == other.ID;

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ID);
}

internal sealed class RunnerJSONConverter : JsonConverter<Runner>
{
    public override Runner Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var profile = JsonSerializer.Deserialize<Profile>(ref reader, options) ?? throw new JsonException();
        if (profile.ID is null || profile.Name is null || profile.FrameOrder is null)
        {
            throw new JsonException("A custom runner profile has null fields.");
        }
        return new Runner(profile.ID, profile.Name, profile.IsTemplate, profile.FrameOrder);
    }

    public override void Write(Utf8JsonWriter writer, Runner value, JsonSerializerOptions options)
    {
        if (value.Source is not RunnerSource.Custom custom)
        {
            throw new JsonException("Only custom runners are persisted.");
        }
        JsonSerializer.Serialize(writer, new Profile
        {
            ID = value.ID,
            Name = custom.Name,
            IsTemplate = value.IsTemplate,
            FrameOrder = value.FrameOrder,
        }, options);
    }

    private sealed class Profile
    {
        [JsonPropertyName("id")]
        public required string ID { get; init; }
        [JsonPropertyName("name")]
        public required string Name { get; init; }
        [JsonPropertyName("isTemplate")]
        public required bool IsTemplate { get; init; }
        [JsonPropertyName("frameOrder")]
        public required FrameOrder FrameOrder { get; init; }
    }
}
