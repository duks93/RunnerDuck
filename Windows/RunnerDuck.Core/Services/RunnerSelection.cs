/*
 RunnerSelection.cs
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

public sealed class RunnerLoadingException(string runnerID, Exception innerException)
    : Exception($"Loading runner {runnerID} failed: {innerException.Message}", innerException)
{
    public string RunnerID { get; } = runnerID;
}

public sealed record RunnerSelectionResult<TFrames>(Runner Runner, TFrames Frames, RunnerLoadingException? Failure);

public static class RunnerSelection
{
    public static RunnerSelectionResult<TFrames> Select<TFrames>(
        AppSettings settings,
        IReadOnlyList<Runner> customRunners,
        Func<Runner, TFrames> loadFrames
    )
    {
        return Switch(RunnerCatalog.Resolve(settings.RunnerID, customRunners), settings, loadFrames);
    }

    public static RunnerSelectionResult<TFrames> Switch<TFrames>(
        Runner runner,
        AppSettings settings,
        Func<Runner, TFrames> loadFrames
    )
    {
        try
        {
            var frames = loadFrames(runner);
            settings.RunnerID = runner.ID;
            return new(runner, frames, null);
        }
        catch (RunnerLoadingException failure) when (runner != Runner.Default)
        {
            var frames = loadFrames(Runner.Default);
            settings.RunnerID = Runner.Default.ID;
            return new(Runner.Default, frames, failure);
        }
    }
}
