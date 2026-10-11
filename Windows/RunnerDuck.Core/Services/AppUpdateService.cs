/*
 AppUpdateService.cs
 RunnerDuck.Core

 Created by duks93 on 2026/10/11.
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

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunnerDuck.Core;

public enum AppUpdateStatus
{
    NotChecked,
    Checking,
    UpToDate,
    Available,
    UnpublishedBuild,
    UnknownBuild,
    Failed,
}

public sealed record AppUpdateCheck(AppUpdateStatus Status, string? LatestCommit = null);

public sealed class AppUpdateService(HttpClient httpClient, string? installedCommit)
{
    public string? InstalledCommit { get; } = IsCommit(installedCommit) ? installedCommit!.ToLowerInvariant() : null;
    public const string ChangesURL = "https://github.com/duks93/RunnerDuck/releases";

    public async Task<AppUpdateCheck> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (InstalledCommit is null) return new(AppUpdateStatus.UnknownBuild);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://github.com/duks93/RunnerDuck/releases/latest/download/latest.json");
        request.Headers.UserAgent.ParseAdd("RunnerDuck");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.CacheControl = new() { NoCache = true };
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK) return new(AppUpdateStatus.Failed);
            var data = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<Manifest>(data);
            if (manifest is not { SchemaVersion: 1, Ancestors: { } ancestors } ||
                !IsPublishedCommit(manifest.Commit) || !ancestors.All(IsPublishedCommit) || ancestors.Contains(manifest.Commit))
            {
                return new(AppUpdateStatus.Failed);
            }
            if (manifest.Commit == InstalledCommit) return new(AppUpdateStatus.UpToDate);
            return ancestors.Contains(InstalledCommit)
                ? new(AppUpdateStatus.Available, manifest.Commit)
                : new(AppUpdateStatus.UnpublishedBuild);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            return new(AppUpdateStatus.Failed);
        }
    }

    public static string? CommitFromInformationalVersion(string? version)
    {
        var separator = version?.IndexOf('+') ?? -1;
        var commit = separator >= 0 ? version![(separator + 1)..] : null;
        return IsCommit(commit) ? commit!.ToLowerInvariant() : null;
    }

    private static bool IsPublishedCommit(string? value) => IsCommit(value) && value == value!.ToLowerInvariant();

    private static bool IsCommit(string? value) => value is { Length: 40 } && value.All(char.IsAsciiHexDigit);

    private sealed record Manifest(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("commit")] string? Commit,
        [property: JsonPropertyName("ancestors")] string[]? Ancestors
    );
}
