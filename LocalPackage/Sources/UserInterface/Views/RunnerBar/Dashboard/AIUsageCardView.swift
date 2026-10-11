/*
 AIUsageCardView.swift
 UserInterface

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
import SwiftUI

struct AIUsageCardView: View {
    var aiUsageBundles: [AIUsageBundle]
    var expandedIDs: Set<String>
    var displayedDate: Date
    var rowTapped: (String) async -> Void

    private var lastUpdatedDetail: String {
        let date = aiUsageBundles.map(\.lastUpdatedDate).min() ?? displayedDate
        return Date.AnchoredRelativeFormatStyle(anchor: date, presentation: .named).format(displayedDate)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text("aiUsage", bundle: .module)
                Spacer()
                Text(verbatim: lastUpdatedDetail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            ForEach(aiUsageBundles) { aiUsageBundle in
                AIUsageRowView(
                    aiUsageBundle: aiUsageBundle,
                    isExpanded: expandedIDs.contains(aiUsageBundle.id),
                    displayedDate: displayedDate,
                    tapped: { await rowTapped(aiUsageBundle.id) }
                )
            }
        }
        .padding(.horizontal, 8)
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(8)
        .materialCellStyle()
    }
}

private struct AIUsageRowView: View {
    var aiUsageBundle: AIUsageBundle
    var isExpanded: Bool
    var displayedDate: Date
    var tapped: () async -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Button {
                Task { await tapped() }
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: aiUsageBundle.provider.symbolName)
                        .frame(width: 16)
                    Text(verbatim: aiUsageBundle.displayName)
                        .lineLimit(1)
                        .truncationMode(.middle)
                        .frame(width: 150, alignment: .leading)
                    AIUsageSummaryView(aiUsageBundle: aiUsageBundle)
                    Spacer(minLength: .zero)
                    Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
                .contentShape(.rect)
            }
            .buttonStyle(.plain)
            if isExpanded, case let .available(windows) = aiUsageBundle.status {
                ForEach(windows.enumerated(), id: \.offset) { _, window in
                    HStack(spacing: 6) {
                        Text(verbatim: "\(window.title) · \(window.formattedRemaining)")
                        if let resetDetail = window.resetDetail(from: displayedDate) {
                            Text(verbatim: resetDetail)
                                .foregroundStyle(.secondary)
                        }
                    }
                    .font(.caption)
                    .padding(.leading, 22)
                }
            }
        }
    }
}

private struct AIUsageSummaryView: View {
    var aiUsageBundle: AIUsageBundle

    var body: some View {
        switch aiUsageBundle.status {
        case .available:
            HStack(spacing: 8) {
                ForEach(aiUsageBundle.summaryWindows.enumerated(), id: \.offset) { _, window in
                    HStack(spacing: 3) {
                        Text(verbatim: window.shortTitle)
                            .foregroundStyle(.secondary)
                        RemainingBarView(value: window.remainingPercentage)
                        Text(verbatim: window.formattedRemaining)
                            .monospacedDigit()
                            .frame(width: 56, alignment: .leading)
                    }
                }
            }
            .font(.caption)
        case .signInRequired:
            Text("aiUsageSignInRequiredShort", bundle: .module)
                .font(.caption)
                .foregroundStyle(Color.orange)
        case .failed:
            Text("failed", bundle: .module)
                .font(.caption)
                .foregroundStyle(Color.red)
        }
    }
}

struct RemainingBarView: View {
    var value: Double

    var body: some View {
        Capsule()
            .fill(Color.secondary.opacity(0.25))
            .frame(width: 36, height: 5)
            .overlay(alignment: .leading) {
                Capsule()
                    .fill(Color.accentColor)
                    .frame(width: 36 * max(0, min(100, value)) / 100, height: 5)
            }
    }
}
