/*
 AIUsageWidgetView.swift
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
import SwiftUI
import WidgetKit

struct AIUsageWidgetView: View {
    @Environment(\.widgetFamily) private var widgetFamily
    var entry: AIUsageWidgetEntry

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("aiUsage", bundle: .module)
                    .font(.headline)
                Spacer(minLength: .zero)
                Text(entry.lastUpdatedDate, style: .time)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
            if entry.aiUsageBundles.isEmpty {
                Text("aiUsageWidgetEmpty", bundle: .module)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                ForEach(entry.aiUsageBundles.prefix(widgetFamily.maxRowCount)) { aiUsageBundle in
                    if widgetFamily == .systemSmall {
                        AIUsageWidgetCompactRowView(aiUsageBundle: aiUsageBundle)
                    } else {
                        AIUsageWidgetRowView(aiUsageBundle: aiUsageBundle)
                    }
                }
            }
            Spacer(minLength: .zero)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .containerBackground(.fill.tertiary, for: .widget)
    }
}

private struct AIUsageWidgetRowView: View {
    var aiUsageBundle: AIUsageBundle

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: aiUsageBundle.provider.symbolName)
                .frame(width: 16)
            Text(verbatim: aiUsageBundle.displayName)
                .lineLimit(1)
                .truncationMode(.middle)
            Spacer(minLength: .zero)
            switch aiUsageBundle.status {
            case .available:
                ForEach(aiUsageBundle.summaryWindows.enumerated(), id: \.offset) { _, window in
                    HStack(spacing: 3) {
                        Text(verbatim: window.shortTitle)
                            .foregroundStyle(.secondary)
                        RemainingBarView(value: window.remainingPercentage)
                        Text(verbatim: window.formattedRemainingPercentage)
                            .monospacedDigit()
                            .frame(width: 32, alignment: .trailing)
                    }
                    .fixedSize()
                }
            case .signInRequired:
                Text("aiUsageSignInRequiredShort", bundle: .module)
                    .foregroundStyle(Color.orange)
            case .failed:
                Text("failed", bundle: .module)
                    .foregroundStyle(Color.red)
            }
        }
        .font(.caption)
    }
}

private struct AIUsageWidgetCompactRowView: View {
    var aiUsageBundle: AIUsageBundle

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: aiUsageBundle.provider.symbolName)
                .frame(width: 16)
            switch aiUsageBundle.status {
            case .available:
                ForEach(aiUsageBundle.summaryWindows.enumerated(), id: \.offset) { _, window in
                    HStack(spacing: 2) {
                        Text(verbatim: window.shortTitle)
                            .foregroundStyle(.secondary)
                        Text(verbatim: window.formattedRemainingPercentage)
                            .monospacedDigit()
                    }
                }
            case .signInRequired:
                Text("aiUsageSignInRequiredShort", bundle: .module)
                    .foregroundStyle(Color.orange)
            case .failed:
                Text("failed", bundle: .module)
                    .foregroundStyle(Color.red)
            }
        }
        .font(.caption)
        .lineLimit(1)
    }
}

private extension WidgetFamily {
    var maxRowCount: Int {
        switch self {
        case .systemLarge: 12
        default: 4
        }
    }
}
