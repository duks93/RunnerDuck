/*
 AIUsageEnvironment.cs
 RunnerDuck.AIUsage

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

namespace RunnerDuck.AIUsage;

public sealed class AIUsageEnvironment
{
    public required string HomeDirectory { get; init; }
    public required Func<string, byte[]?> ReadFile { get; init; }
    public required Func<string, bool> PathExists { get; init; }
    public required HttpClient Http { get; init; }
    public required Func<DateTimeOffset> Now { get; init; }
    public Action<string> Log { get; init; } = _ => { };

    public static AIUsageEnvironment Live(Action<string> log) => new()
    {
        HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ReadFile = path =>
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        },
        PathExists = path => Directory.Exists(path) || File.Exists(path),
        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
        Now = () => DateTimeOffset.Now,
        Log = log,
    };
}
