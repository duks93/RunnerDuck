/*
 AIUsageWindow.swift
 DataSource

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

import Foundation

public struct AIUsageWindow: Sendable, Equatable, Codable {
    public var kind: Kind
    public var percentage: Double
    public var resetDate: Date?

    public init(kind: Kind, percentage: Double, resetDate: Date? = nil) {
        self.kind = kind
        self.percentage = percentage
        self.resetDate = resetDate
    }

    public enum Kind: Sendable, Equatable, Codable {
        case fiveHour
        case weekly
        case monthly
        case weeklyModel(String)
    }
}
