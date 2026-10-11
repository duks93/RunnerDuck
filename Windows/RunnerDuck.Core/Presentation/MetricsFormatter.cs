/*
 MetricsFormatter.cs
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

using System.Globalization;

namespace RunnerDuck.Core;

public static class MetricsFormatter
{
    public static string FormatPercentage(Percentage percentage, IFormatProvider? provider = null) =>
        Number(percentage.Value, percentage.Value < 100 ? 1 : 0, percentage.Width, provider) + "%";

    public static string FormatBytes(ByteData bytes, IFormatProvider? provider = null)
    {
        var (value, unit) = bytes.ReadableValue;
        return $"{Number(value, 1, 4, provider)} {unit}";
    }

    public static string FormatStorage(ByteData bytes, IFormatProvider? provider = null)
    {
        var (value, unit) = bytes.ReadableValue;
        return $"{Number(value, 2, 5, provider)} {unit}";
    }

    public static string FormatSpeed(ByteData bytesPerSecond, IFormatProvider? provider = null)
    {
        var (value, unit) = bytesPerSecond.ReadableValue;
        return $"{Number(value, value < 100 ? 1 : 0, 4, provider)} {unit}/s";
    }

    public static string FormatCompactPercentage(Percentage percentage, IFormatProvider? provider = null) =>
        Math.Round(percentage.Value, MidpointRounding.AwayFromZero).ToString("F0", provider ?? CultureInfo.CurrentCulture);

    public static string FormatCompactSpeed(ByteData bytesPerSecond, IFormatProvider? provider = null)
    {
        var (value, unit) = bytesPerSecond.ReadableValue;
        var decimals = CompactDecimals(value, unit);
        if (Math.Round(value, decimals, MidpointRounding.AwayFromZero) >= 1000 && ByteData.NextUnit(unit) is { } nextUnit)
        {
            value /= 1000;
            unit = nextUnit;
            decimals = CompactDecimals(value, unit);
        }
        var suffix = unit == "B" ? "B" : unit[..1].ToUpperInvariant();
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString($"F{decimals}", provider ?? CultureInfo.CurrentCulture) + suffix;
    }

    private static int CompactDecimals(double value, string unit) =>
        unit != "B" && Math.Round(value, 1, MidpointRounding.AwayFromZero) < 10 ? 1 : 0;

    private static string Number(double value, int decimals, int width, IFormatProvider? provider) =>
        value.ToString($"F{decimals}", provider ?? CultureInfo.CurrentCulture).PadLeft(width);
}
