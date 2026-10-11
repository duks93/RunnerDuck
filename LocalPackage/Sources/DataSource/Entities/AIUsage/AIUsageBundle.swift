/*
 AIUsageBundle.swift
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

public struct AIUsageBundle: Sendable, Identifiable, Equatable, Codable {
    public var provider: AIUsageProvider
    public var accountID: String?
    public var accountName: String?
    public var status: Status
    public var lastUpdatedDate: Date

    public var id: String {
        "\(provider.rawValue)/\(accountID ?? "local")"
    }

    public var summaryWindows: [AIUsageWindow] {
        guard case let .available(windows) = status else { return [] }
        let fiveHour = windows.first { $0.kind == .fiveHour }
        let longest = windows.first { $0.kind == .weekly } ?? windows.first { $0.kind == .monthly }
        return [fiveHour, longest].compactMap(\.self)
    }

    public init(
        provider: AIUsageProvider,
        accountID: String? = nil,
        accountName: String? = nil,
        status: Status,
        lastUpdatedDate: Date
    ) {
        self.provider = provider
        self.accountID = accountID
        self.accountName = accountName
        self.status = status
        self.lastUpdatedDate = lastUpdatedDate
    }

    public enum Status: Sendable, Equatable, Codable {
        case available([AIUsageWindow])
        case signInRequired
        case failed
    }
}
