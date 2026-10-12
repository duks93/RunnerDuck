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
public enum RunnerKind { Duck, DuckFilled, Penguin, Chicken, Dinosaur, Owl, Cat, Dog, Pig, Sheep, Fox, Turtle }
public static class RunnerKindExtensions
{
    public static string ID(this RunnerKind kind) => kind switch
    {
        RunnerKind.Duck => "duck",
        RunnerKind.DuckFilled => "duck-filled",
        RunnerKind.Penguin => "penguin",
        RunnerKind.Chicken => "chicken",
        RunnerKind.Dinosaur => "dinosaur",
        RunnerKind.Owl => "owl",
        RunnerKind.Cat => "cat",
        RunnerKind.Dog => "dog",
        RunnerKind.Pig => "pig",
        RunnerKind.Sheep => "sheep",
        RunnerKind.Fox => "fox",
        RunnerKind.Turtle => "turtle",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static bool IsTemplate(this RunnerKind kind) => kind switch
    {
        RunnerKind.Duck => true,
        RunnerKind.DuckFilled or RunnerKind.Penguin or RunnerKind.Chicken or RunnerKind.Dinosaur or RunnerKind.Owl
            or RunnerKind.Cat or RunnerKind.Dog or RunnerKind.Pig or RunnerKind.Sheep or RunnerKind.Fox or RunnerKind.Turtle => false,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static int NumberOfResources(this RunnerKind kind) => kind switch
    {
        RunnerKind.Duck or RunnerKind.DuckFilled or RunnerKind.Owl or RunnerKind.Pig or RunnerKind.Turtle => 8,
        RunnerKind.Penguin or RunnerKind.Chicken or RunnerKind.Sheep => 10,
        RunnerKind.Dinosaur or RunnerKind.Cat or RunnerKind.Dog or RunnerKind.Fox => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static FrameOrder FrameOrder(this RunnerKind kind) => Core.FrameOrder.Ascending(kind.NumberOfResources());
}
