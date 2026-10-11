// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace RunnerDuck.Core;

public enum RunnerSize { Small = 75, Standard = 100, Large = 125 }

public static class RunnerSizeExtensions
{
    public static double Scale(this RunnerSize size) => size switch
    {
        RunnerSize.Small => 0.75,
        RunnerSize.Large => 1.25,
        _ => 1.0,
    };
}
