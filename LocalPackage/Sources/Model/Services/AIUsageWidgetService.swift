/*
 AIUsageWidgetService.swift
 Model

 Created by duks93 on 2026/10/04.
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
import Foundation

public struct AIUsageWidgetService: Sendable {
    public static let reloadInterval = TimeInterval(900)

    private let aiUsageSnapshotRepository: AIUsageSnapshotRepository
    private let logService: LogService
    private let widgetCenterClient: WidgetCenterClient

    public init(_ appDependencies: AppDependencies) {
        self.aiUsageSnapshotRepository = .init(appDependencies.dataClient, appDependencies.fileManagerClient)
        self.logService = .init(appDependencies)
        self.widgetCenterClient = appDependencies.widgetCenterClient
    }

    public func loadBundles() -> [AIUsageBundle] {
        aiUsageSnapshotRepository.loadBundles()
    }

    func publish(_ bundles: [AIUsageBundle]) {
        let previousBundles = aiUsageSnapshotRepository.loadBundles()
        do {
            try aiUsageSnapshotRepository.saveBundles(bundles)
        } catch {
            logService.error(.savingAIUsageSnapshotFailed(error))
            return
        }
        guard Self.usages(of: previousBundles) != Self.usages(of: bundles) else { return }
        widgetCenterClient.reloadTimelines(.aiUsageWidgetKind)
    }

    static func usages(of bundles: [AIUsageBundle]) -> [AIUsageBundle] {
        bundles.map { bundle in
            var bundle = bundle
            bundle.lastUpdatedDate = .distantPast
            return bundle
        }
    }
}
