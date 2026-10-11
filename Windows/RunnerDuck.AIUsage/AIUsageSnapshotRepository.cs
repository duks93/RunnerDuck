/*
 AIUsageSnapshotRepository.cs
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunnerDuck.AIUsage;

public sealed record AIUsageSnapshot(DateTimeOffset SavedAt, IReadOnlyList<AIUsageBundle> Bundles)
{
    public static readonly TimeSpan FreshnessInterval = TimeSpan.FromMinutes(6);

    public bool IsFresh(DateTimeOffset now)
    {
        var age = now - SavedAt;
        return age >= TimeSpan.Zero && age <= FreshnessInterval;
    }

    public bool Equals(AIUsageSnapshot? other) =>
        other is not null && SavedAt == other.SavedAt && Bundles.SequenceEqual(other.Bundles);

    public override int GetHashCode() => HashCode.Combine(SavedAt, Bundles.Count);
}

public sealed class AIUsageSnapshotRepository(string filePath)
{
    public const string FileName = "ai-usage-snapshot.json";

    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public AIUsageSnapshot? Load()
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<AIUsageSnapshot>(File.ReadAllBytes(filePath), options);
            return snapshot is null || snapshot.Bundles.Any(bundle => bundle is null || bundle.Windows.Any(window => window is null))
                ? null
                : snapshot;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(AIUsageSnapshot snapshot)
    {
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(snapshot, options));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
