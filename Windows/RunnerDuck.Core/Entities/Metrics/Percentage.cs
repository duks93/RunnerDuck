/*
 Percentage.cs
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

namespace RunnerDuck.Core;

public readonly record struct Percentage
{
    private readonly int? width;

    public double RawValue { get; }
    public int Width => width ?? 4;
    public double Value => Math.Round(1000d * RawValue, MidpointRounding.AwayFromZero) / 10d;

    public Percentage(double rawValue, int width = 4)
    {
        RawValue = rawValue;
        this.width = width;
    }

    public static Percentage Zero => new(0);
}
