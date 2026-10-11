/*
 RunnerFrameStore.cs
 RunnerDuck.App

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

using System.Runtime.InteropServices;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tray;

internal sealed class RunnerFrameStore
{
    public IReadOnlyList<Bitmap> LoadFrames(Runner runner)
    {
        var cache = new Dictionary<string, Bitmap>();
        try
        {
            if (runner.Source is RunnerSource.BuiltIn builtIn) return DuckAssets.LoadFrames(builtIn.Kind);
            return runner.ResourceNames()
                .Select(name =>
                {
                    if (!cache.TryGetValue(name, out var frame))
                    {
                        frame = LoadBitmap(FramePath(runner, name));
                        cache[name] = frame;
                    }
                    return frame;
                })
                .ToList();
        }
        catch (Exception exception) when (IsLoadingFailure(exception))
        {
            foreach (var frame in cache.Values)
            {
                frame.Dispose();
            }
            throw new RunnerLoadingException(runner.ID, exception);
        }
    }

    public Bitmap? LoadThumbnail(Runner runner)
    {
        try
        {
            if (runner.Source is RunnerSource.BuiltIn builtIn)
            {
                return DuckAssets.LoadThumbnail(builtIn.Kind);
            }
            return LoadBitmap(FramePath(runner, runner.ResourceNames()[0]));
        }
        catch (Exception exception) when (IsLoadingFailure(exception))
        {
            return null;
        }
    }

    private static bool IsLoadingFailure(Exception exception) =>
        exception is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException;

    private static string FramePath(Runner runner, string resourceName) =>
        Path.Combine(AppPaths.CustomRunnersDirectory, runner.ID, $"{resourceName}.png");

    private static Bitmap LoadBitmap(string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

}
