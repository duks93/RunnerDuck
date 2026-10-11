/*
 AIUsageWidget.swift
 UserInterface

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
import Model
import SwiftUI
import WidgetKit

public struct AIUsageWidget: Widget {
    public init() {}

    public var body: some WidgetConfiguration {
        StaticConfiguration(kind: .aiUsageWidgetKind, provider: AIUsageTimelineProvider()) { entry in
            AIUsageWidgetView(entry: entry)
        }
        .configurationDisplayName(Text("aiUsage", bundle: .module))
        .description(Text("aiUsageWidgetDescription", bundle: .module))
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge])
    }
}

struct AIUsageWidgetEntry: TimelineEntry {
    var date: Date
    var aiUsageBundles: [AIUsageBundle]

    var lastUpdatedDate: Date {
        aiUsageBundles.map(\.lastUpdatedDate).min() ?? date
    }

    static let placeholder = AIUsageWidgetEntry(
        date: .now,
        aiUsageBundles: AIUsageProvider.allCases.map { provider in
            AIUsageBundle(
                provider: provider,
                status: .available([
                    AIUsageWindow(kind: .fiveHour, percentage: 25),
                    AIUsageWindow(kind: .weekly, percentage: 60),
                ]),
                lastUpdatedDate: .now
            )
        }
    )
}

struct AIUsageTimelineProvider: TimelineProvider {
    private var aiUsageWidgetService: AIUsageWidgetService {
        AIUsageWidgetService(EnvironmentValues().appDependencies)
    }

    func placeholder(in context: Context) -> AIUsageWidgetEntry {
        .placeholder
    }

    func getSnapshot(in context: Context, completion: @escaping (AIUsageWidgetEntry) -> Void) {
        completion(context.isPreview ? .placeholder : currentEntry())
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<AIUsageWidgetEntry>) -> Void) {
        let entry = currentEntry()
        let nextReloadDate = entry.date.addingTimeInterval(AIUsageWidgetService.reloadInterval)
        completion(Timeline(entries: [entry], policy: .after(nextReloadDate)))
    }

    private func currentEntry() -> AIUsageWidgetEntry {
        AIUsageWidgetEntry(date: .now, aiUsageBundles: aiUsageWidgetService.loadBundles())
    }
}
