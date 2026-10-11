/*
 OpenCodexResponses.swift
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

public struct OpenCodexRuntimePort: Decodable, Sendable {
    public var port: Int
}

public struct OpenCodexAccountsResponse: Decodable, Sendable {
    public var accounts: [Account]?

    public struct Account: Decodable, Sendable {
        public var id: String
        public var alias: String?
        public var email: String?
        public var isMain: Bool?
        public var needsReauth: Bool?
        public var quotaUnavailable: Bool?
        public var quota: Quota?
    }

    public struct Quota: Decodable, Sendable {
        public var fiveHourPercent: FlexibleNumber?
        public var fiveHourResetAt: FlexibleNumber?
        public var shortPercent: FlexibleNumber?
        public var shortResetAt: FlexibleNumber?
        public var weeklyPercent: FlexibleNumber?
        public var weeklyResetAt: FlexibleNumber?
        public var monthlyPercent: FlexibleNumber?
        public var monthlyResetAt: FlexibleNumber?
        public var customWindows: [CustomWindow]?
    }

    public struct CustomWindow: Decodable, Sendable {
        public var label: String
        public var percent: FlexibleNumber?
        public var resetAt: FlexibleNumber?
    }
}
