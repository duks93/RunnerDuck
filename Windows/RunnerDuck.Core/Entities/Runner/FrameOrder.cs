/*
 FrameOrder.cs
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

[JsonConverter(typeof(FrameOrderJSONConverter))]
public sealed class FrameOrder
{
    private FrameOrder(IEnumerable<int> order)
    {
        Order = Array.AsReadOnly(order.ToArray());
    }

    public IReadOnlyList<int> Order { get; }

    public int this[int index] => index < Order.Count ? Order[index] : 0;

    public static FrameOrder Ascending(int count) => new(Enumerable.Range(0, count));
    public static FrameOrder Swing { get; } = new([0, 1, 2, 3, 4, 3, 2, 1]);
    public static FrameOrder Pendulum { get; } = new([0, 1, 2, 1, 0, 3, 4, 3]);
    public static FrameOrder PartyHorn { get; } = new([0, 1, 2, 3, 4, 4, 3, 2, 1]);
    public static FrameOrder Custom(IEnumerable<int> order) => new(order);
}

internal sealed class FrameOrderJSONConverter : JsonConverter<FrameOrder>
{
    public override FrameOrder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        FrameOrder.Custom(JsonSerializer.Deserialize<int[]>(ref reader, options) ?? throw new JsonException());

    public override void Write(Utf8JsonWriter writer, FrameOrder value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Order, options);
}
