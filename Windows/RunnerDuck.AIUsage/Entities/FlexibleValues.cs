/*
 FlexibleValues.cs
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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunnerDuck.AIUsage;

[JsonConverter(typeof(FlexibleNumberConverter))]
public readonly record struct FlexibleNumber(double? Value);

[JsonConverter(typeof(FlexibleDateConverter))]
public readonly record struct FlexibleDate(string? RawValue, double? Number);

internal sealed class FlexibleNumberConverter : JsonConverter<FlexibleNumber>
{
    public override FlexibleNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return new FlexibleNumber(reader.GetDouble());
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim();
                return new FlexibleNumber(double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null);
            default:
                reader.Skip();
                return new FlexibleNumber(null);
        }
    }

    public override void Write(Utf8JsonWriter writer, FlexibleNumber value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}

internal sealed class FlexibleDateConverter : JsonConverter<FlexibleDate>
{
    public override FlexibleDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return new FlexibleDate(null, reader.GetDouble());
            case JsonTokenType.String:
                return new FlexibleDate(reader.GetString(), null);
            default:
                reader.Skip();
                return new FlexibleDate(null, null);
        }
    }

    public override void Write(Utf8JsonWriter writer, FlexibleDate value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
