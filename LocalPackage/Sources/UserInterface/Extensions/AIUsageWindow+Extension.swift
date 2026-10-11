/*
 AIUsageWindow+Extension.swift
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
import Foundation

extension AIUsageWindow {
    var title: String {
        switch kind {
        case .fiveHour:
            String(localized: "aiUsageFiveHour", bundle: .module)
        case .weekly:
            String(localized: "aiUsageWeekly", bundle: .module)
        case .monthly:
            String(localized: "aiUsageMonthly", bundle: .module)
        case let .weeklyModel(model):
            String(localized: "aiUsageWeeklyModel:\(model)", bundle: .module)
        }
    }

    var shortTitle: String {
        switch kind {
        case .fiveHour:
            String(localized: "aiUsageFiveHourShort", bundle: .module)
        case .weekly:
            String(localized: "aiUsageWeeklyShort", bundle: .module)
        case .monthly:
            String(localized: "aiUsageMonthlyShort", bundle: .module)
        case let .weeklyModel(model):
            model
        }
    }

    var remainingPercentage: Double {
        100 - percentage
    }

    var formattedRemainingPercentage: String {
        (remainingPercentage / 100).formatted(.percent.precision(.fractionLength(0)))
    }

    var formattedRemaining: String {
        String(localized: "aiUsageRemaining:\(formattedRemainingPercentage)", bundle: .module)
    }

    func resetDetail(from date: Date) -> String? {
        guard let resetDate else { return nil }
        let relative = Date.AnchoredRelativeFormatStyle(anchor: resetDate, presentation: .named).format(date)
        return String(localized: "aiUsageResets:\(relative)", bundle: .module)
    }
}
