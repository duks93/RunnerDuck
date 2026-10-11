/*
 RunnerKind.cs
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
public enum RunnerKind { Duck, DuckFilled }
public static class RunnerKindExtensions
{
    public static string ID(this RunnerKind kind) => kind switch
    {
        RunnerKind.Duck => "duck",
        RunnerKind.DuckFilled => "duck-filled",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static bool IsTemplate(this RunnerKind kind) => kind switch
    {
        RunnerKind.Duck => true,
        RunnerKind.DuckFilled => false,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static int NumberOfResources(this RunnerKind kind) => kind is RunnerKind.Duck or RunnerKind.DuckFilled ? 8 : throw new ArgumentOutOfRangeException(nameof(kind));
    public static FrameOrder FrameOrder(this RunnerKind kind) => Core.FrameOrder.Ascending(kind.NumberOfResources());
}
