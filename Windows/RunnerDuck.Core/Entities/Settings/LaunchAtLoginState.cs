/*
 LaunchAtLoginState.cs
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

public enum LaunchAtLoginState
{
    Unavailable,
    Disabled,
    Enabled,
    DisabledByUser,
    DisabledByPolicy,
    EnabledByPolicy,
}

public enum LaunchAtLoginNote
{
    None,
    TurnOnInTaskManager,
    ManagedByPolicy,
    Unavailable,
}

public static class LaunchAtLoginStateExtensions
{
    public static bool IsOn(this LaunchAtLoginState state) =>
        state is LaunchAtLoginState.Enabled or LaunchAtLoginState.EnabledByPolicy;

    public static bool CanChange(this LaunchAtLoginState state) =>
        state is LaunchAtLoginState.Enabled or LaunchAtLoginState.Disabled;

    public static LaunchAtLoginNote Note(this LaunchAtLoginState state) => state switch
    {
        LaunchAtLoginState.DisabledByUser => LaunchAtLoginNote.TurnOnInTaskManager,
        LaunchAtLoginState.DisabledByPolicy or LaunchAtLoginState.EnabledByPolicy => LaunchAtLoginNote.ManagedByPolicy,
        LaunchAtLoginState.Unavailable => LaunchAtLoginNote.Unavailable,
        _ => LaunchAtLoginNote.None,
    };
}
