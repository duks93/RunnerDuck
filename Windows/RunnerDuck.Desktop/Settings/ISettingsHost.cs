/*
 ISettingsHost.cs
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

using RunnerDuck.AIUsage;
using RunnerDuck.Core;

namespace RunnerDuck.App.Settings;

internal enum SettingsChange
{
    UpdateInterval,
    Monitoring,
    MetricsBar,
    RunnerAppearance,
    RunnerSpeed,
    AIUsageSources,
}

internal interface ISettingsHost
{
    AppSettings Settings { get; }

    Runner CurrentRunner { get; }

    IReadOnlyList<Runner> Runners();

    void SelectRunner(Runner runner);

    IReadOnlyList<Bitmap> PreviewFrames(Runner runner, int height, Color color);

    Image? Thumbnail(Runner runner);

    IReadOnlyList<AIUsageSourceState> AIUsageSourceStates();

    IReadOnlyList<Runner> CustomRunners();

    Runner AddCustomRunner(string name, bool isTemplate, IReadOnlyList<byte[]> pngFrames);

    void DeleteCustomRunner(Runner runner);

    void MoveCustomRunner(int fromIndex, int toIndex);

    IReadOnlyList<CustomMetricsSource> CustomMetricsSources();

    bool IsCustomMetricsSourceFailed(Guid sourceID);

    void AddCustomMetricsSource(string filePath);

    void RemoveCustomMetricsSource(Guid sourceID);

    void MoveCustomMetricsSource(int fromIndex, int toIndex);

    void SettingsChanged(SettingsChange change);
}
