/*
 AIUsageSettings.swift
 Model

 Created by duks93 on 2026/09/25.
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

import DataSource
import Observation

@MainActor @Observable
public final class AIUsageSettings: Composable {
    private let aiUsageService: AIUsageService

    public var sourceStates: [AIUsageSourceState]
    public let action: (Action) async -> Void

    public init(
        _ appDependencies: AppDependencies,
        sourceStates: [AIUsageSourceState] = [],
        action: @escaping (Action) async -> Void = { _ in }
    ) {
        self.aiUsageService = .init(appDependencies)
        self.sourceStates = sourceStates
        self.action = action
    }

    public func reduce(_ action: Action) async {
        switch action {
        case .viewAppeared:
            sourceStates = aiUsageService.sourceStates()

        case let .sourceToggleSwitched(source, isOn):
            aiUsageService.setSource(source, isEnabled: isOn)
            sourceStates = aiUsageService.sourceStates()
        }
    }

    public enum Action: Sendable {
        case viewAppeared
        case sourceToggleSwitched(AIUsageSource, Bool)
    }
}
