// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace RunnerDuck.Core;

public static class TrayToolTip
{
    public static string Compose(IReadOnlyList<string> lines, int maximumLength)
    {
        if (lines.Count == 0)
        {
            return "";
        }
        var first = lines[0];
        if (first.Length > maximumLength)
        {
            return first[..maximumLength];
        }
        var result = new StringBuilder(first);
        foreach (var line in lines.Skip(1))
        {
            if (result.Length + 1 + line.Length > maximumLength)
            {
                break;
            }
            result.Append('\n').Append(line);
        }
        return result.ToString();
    }
}
