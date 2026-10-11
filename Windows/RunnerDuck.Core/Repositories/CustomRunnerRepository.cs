/*
 CustomRunnerRepository.cs
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

public sealed class CustomRunnerRepository(string filePath)
{
    public const string FileName = "customRunners.json";

    private readonly JSONFileStore<List<Runner>> store = new(filePath);

    public IReadOnlyList<Runner> Load()
    {
        var runners = store.Load(() => []);
        return runners.Any(runner => runner is null) ? [] : runners;
    }

    public Runner? Load(string runnerID) => Load().FirstOrDefault(runner => runner.ID == runnerID);

    public void Save(IEnumerable<Runner> runners) => store.Save(runners.ToList());
}
