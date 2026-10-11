/*
 CustomRunnerService.cs
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

public enum CustomRunnerError
{
    NameEmpty,
    NameAlreadyExists,
    NoFrames,
    InvalidFrameImage,
    FrameLimitExceeded,
    SavingFailed,
    RunnerInUse,
}

public sealed class CustomRunnerException(CustomRunnerError error) : Exception(error.ToString())
{
    public CustomRunnerError Error { get; } = error;
}

public static class CustomRunnerRules
{
    public const int FrameHeight = 36;
    public const int MinimumFrameWidth = 10;
    public const int MaximumFrameWidth = 100;
    public const int MaximumFrameCount = 30;

    public static bool IsValidFrame(int width, int height) =>
        height == FrameHeight && width is >= MinimumFrameWidth and <= MaximumFrameWidth;

    public static bool CanAddFrame(int currentFrameCount) => currentFrameCount < MaximumFrameCount;

    public static CustomRunnerError? ValidateName(string name, IReadOnlyList<Runner> customRunners)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRunnerError.NameEmpty;
        }
        var isTaken = customRunners.Any(runner => runner.Source is RunnerSource.Custom custom && custom.Name == name);
        return isTaken ? CustomRunnerError.NameAlreadyExists : null;
    }
}

public sealed class CustomRunnerFileSystem
{
    public required Action<string, byte[]> WriteFile { get; init; }
    public required Action<string> DeleteDirectory { get; init; }

    public static CustomRunnerFileSystem Live { get; } = new()
    {
        WriteFile = (path, data) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        },
        DeleteDirectory = path =>
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        },
    };
}

public sealed class CustomRunnerService(
    CustomRunnerRepository repository,
    string runnersDirectory,
    CustomRunnerFileSystem fileSystem,
    Func<Guid> newID
)
{
    public IReadOnlyList<Runner> Runners() => repository.Load();

    public Runner Add(string name, bool isTemplate, IReadOnlyList<byte[]> pngFrames)
    {
        var runners = repository.Load();
        if (CustomRunnerRules.ValidateName(name, runners) is { } nameError)
        {
            throw new CustomRunnerException(nameError);
        }
        if (pngFrames.Count == 0)
        {
            throw new CustomRunnerException(CustomRunnerError.NoFrames);
        }
        if (pngFrames.Count > CustomRunnerRules.MaximumFrameCount)
        {
            throw new CustomRunnerException(CustomRunnerError.FrameLimitExceeded);
        }
        var runner = new Runner(newID().ToString().ToUpperInvariant(), name, isTemplate, FrameOrder.Ascending(pngFrames.Count));
        try
        {
            for (var index = 0; index < pngFrames.Count; index++)
            {
                fileSystem.WriteFile(FramePath(runner, index), pngFrames[index]);
            }
            repository.Save([.. runners, runner]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteDirectory(runner);
            throw new CustomRunnerException(CustomRunnerError.SavingFailed);
        }
        return runner;
    }

    public void Delete(Runner runner, Runner currentRunner)
    {
        if (runner == currentRunner)
        {
            throw new CustomRunnerException(CustomRunnerError.RunnerInUse);
        }
        SaveRunners(repository.Load().Where(existing => existing != runner));
        TryDeleteDirectory(runner);
    }

    public void Move(int fromIndex, int toIndex)
    {
        var runners = repository.Load().ToList();
        if (fromIndex < 0 || fromIndex >= runners.Count || toIndex < 0 || toIndex >= runners.Count || fromIndex == toIndex)
        {
            return;
        }
        var runner = runners[fromIndex];
        runners.RemoveAt(fromIndex);
        runners.Insert(toIndex, runner);
        SaveRunners(runners);
    }

    private void SaveRunners(IEnumerable<Runner> runners)
    {
        try
        {
            repository.Save(runners);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CustomRunnerException(CustomRunnerError.SavingFailed);
        }
    }

    public string FramePath(Runner runner, int frameIndex) => Path.Combine(runnersDirectory, runner.ID, $"frame-{frameIndex}.png");

    private void TryDeleteDirectory(Runner runner)
    {
        try
        {
            fileSystem.DeleteDirectory(Path.Combine(runnersDirectory, runner.ID));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (char.IsAsciiDigit(left[leftIndex]) && char.IsAsciiDigit(right[rightIndex]))
            {
                var leftNumber = ReadNumber(left, ref leftIndex);
                var rightNumber = ReadNumber(right, ref rightIndex);
                var numberComparison = CompareNumbers(leftNumber, rightNumber);
                if (numberComparison != 0)
                {
                    return numberComparison;
                }
                continue;
            }
            var characterComparison = char.ToUpperInvariant(left[leftIndex]).CompareTo(char.ToUpperInvariant(right[rightIndex]));
            if (characterComparison != 0)
            {
                return characterComparison;
            }
            leftIndex++;
            rightIndex++;
        }
        var remainderComparison = (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        return remainderComparison != 0 ? remainderComparison : string.CompareOrdinal(left, right);
    }

    private static string ReadNumber(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }
        return text[start..index].TrimStart('0');
    }

    private static int CompareNumbers(string left, string right) =>
        left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
}
