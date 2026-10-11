/*
 CustomMetricsEnvironment.cs
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

using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace RunnerDuck.Core;

public sealed class CustomMetricsEnvironment
{
    public required Func<string, byte[]> ReadFile { get; init; }
    public required Func<string, bool> FileExists { get; init; }
    // Yields once right after the watch starts and on every change; completes when the file is missing, deleted, or renamed.
    public required Func<string, CancellationToken, IAsyncEnumerable<DateTimeOffset>> Watch { get; init; }
    public required Func<TimeSpan, CancellationToken, Task> Delay { get; init; }
    public required Func<DateTimeOffset> Now { get; init; }
    public required Func<Guid> NewID { get; init; }
    public Action<string> Log { get; init; } = _ => { };

    public static CustomMetricsEnvironment Live(Action<string> log) => new()
    {
        ReadFile = File.ReadAllBytes,
        FileExists = File.Exists,
        Watch = WatchFile,
        Delay = Task.Delay,
        Now = () => DateTimeOffset.Now,
        NewID = Guid.NewGuid,
        Log = log,
    };

    private static async IAsyncEnumerable<DateTimeOffset> WatchFile(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var fullPath = Path.GetFullPath(filePath);
        var channel = Channel.CreateBounded<DateTimeOffset>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        watcher.Changed += (_, _) => channel.Writer.TryWrite(DateTimeOffset.Now);
        watcher.Created += (_, _) => channel.Writer.TryWrite(DateTimeOffset.Now);
        watcher.Deleted += (_, _) => channel.Writer.TryComplete();
        watcher.Renamed += (_, arguments) =>
        {
            if (string.Equals(arguments.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                channel.Writer.TryWrite(DateTimeOffset.Now);
            }
            else
            {
                channel.Writer.TryComplete();
            }
        };
        watcher.Error += (_, _) => channel.Writer.TryComplete();
        watcher.EnableRaisingEvents = true;
        if (File.Exists(fullPath))
        {
            channel.Writer.TryWrite(DateTimeOffset.Now);
        }
        else
        {
            channel.Writer.TryComplete();
        }
        await foreach (var date in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return date;
        }
    }
}
