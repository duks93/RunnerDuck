/*
 AppSettings.cs
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

using RunnerDuck.AIUsage;

namespace RunnerDuck.Core;

public sealed class AppSettings
{
    private UpdateInterval updateInterval = UpdateInterval.FiveSeconds;
    private RunnerSize runnerSize = RunnerSize.Standard;

    public string RunnerID { get; set; } = "duck";
    public bool SpeedDecreasesUnderLoad { get; set; }
    public bool IsFlippedHorizontally { get; set; }
    public RunnerSize RunnerSize
    {
        get => runnerSize;
        set => runnerSize = Enum.IsDefined(value) ? value : RunnerSize.Standard;
    }
    public UpdateInterval UpdateInterval
    {
        get => updateInterval;
        set => updateInterval = Enum.IsDefined(value) ? value : UpdateInterval.FiveSeconds;
    }

    public SystemMetricsConfiguration SystemMetricsConfiguration { get; set; } = new();
    public bool ShowsMetricsBar { get; set; }
    public MetricsBarConfiguration MetricsBarConfiguration { get; set; } = new();
    public CustomMetricsConfiguration CustomMetricsConfiguration { get; set; } = new();
    public AIUsageConfiguration AIUsageConfiguration { get; set; } = new();
}
