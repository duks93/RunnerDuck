/*
 Log.cs
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

namespace RunnerDuck.App;

internal static class Log
{
    private const long MaxFileSize = 1_000_000;

    private static readonly Lock gate = new();
    private static string fileName = "App.log";

    public static void Configure(string logFileName)
    {
        lock (gate)
        {
            fileName = logFileName;
        }
    }

    public static void Write(string message)
    {
        lock (gate)
        {
            try
            {
                var path = Path.Combine(AppPaths.DataDirectory, fileName);
                if (File.Exists(path) && new FileInfo(path).Length > MaxFileSize)
                {
                    File.Move(path, Path.ChangeExtension(path, ".old.log"), overwrite: true);
                }
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
