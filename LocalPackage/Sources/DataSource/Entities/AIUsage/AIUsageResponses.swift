/*
 AIUsageResponses.swift
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

public struct ClaudeCodeCredentials: Decodable, Sendable {
    public var claudeAiOauth: OAuth

    public struct OAuth: Decodable, Sendable {
        public var accessToken: String
        public var expiresAt: Double?
    }
}

public struct ClaudeCodeUsageResponse: Decodable, Sendable {
    public var fiveHour: Bucket?
    public var sevenDay: Bucket?
    public var sevenDayFable: Bucket?
    public var sevenDayOpus: Bucket?
    public var sevenDaySonnet: Bucket?
    public var limits: [Limit]?

    enum CodingKeys: String, CodingKey {
        case fiveHour = "five_hour"
        case sevenDay = "seven_day"
        case sevenDayFable = "seven_day_fable"
        case sevenDayOpus = "seven_day_opus"
        case sevenDaySonnet = "seven_day_sonnet"
        case limits
    }

    public struct Bucket: Decodable, Sendable {
        public var utilization: FlexibleNumber?
        public var resetsAt: String?

        enum CodingKeys: String, CodingKey {
            case utilization
            case resetsAt = "resets_at"
        }
    }

    public struct Limit: Decodable, Sendable {
        public var kind: String?
        public var percent: FlexibleNumber?
        public var resetsAt: String?
        public var scope: Scope?

        enum CodingKeys: String, CodingKey {
            case kind
            case percent
            case resetsAt = "resets_at"
            case scope
        }
    }

    public struct Scope: Decodable, Sendable {
        public var model: Model?
    }

    public struct Model: Decodable, Sendable {
        public var displayName: String?

        enum CodingKeys: String, CodingKey {
            case displayName = "display_name"
        }
    }
}

public struct CodexAuth: Decodable, Sendable {
    public var tokens: Tokens?

    public struct Tokens: Decodable, Sendable {
        public var accessToken: String
        public var accountID: String?

        enum CodingKeys: String, CodingKey {
            case accessToken = "access_token"
            case accountID = "account_id"
        }
    }
}

public struct CodexUsageResponse: Decodable, Sendable {
    public var rateLimit: RateLimit?

    enum CodingKeys: String, CodingKey {
        case rateLimit = "rate_limit"
    }

    public struct RateLimit: Decodable, Sendable {
        public var primaryWindow: Window?
        public var secondaryWindow: Window?
        public var tertiaryWindow: Window?

        enum CodingKeys: String, CodingKey {
            case primaryWindow = "primary_window"
            case secondaryWindow = "secondary_window"
            case tertiaryWindow = "tertiary_window"
        }
    }

    public struct Window: Decodable, Sendable {
        public var usedPercent: FlexibleNumber?
        public var resetAt: FlexibleNumber?
        public var limitWindowSeconds: FlexibleNumber?

        enum CodingKeys: String, CodingKey {
            case usedPercent = "used_percent"
            case resetAt = "reset_at"
            case limitWindowSeconds = "limit_window_seconds"
        }
    }
}

public struct GrokAuthEntry: Decodable, Sendable {
    public var key: String
    public var userID: String?

    enum CodingKeys: String, CodingKey {
        case key
        case userID = "user_id"
    }
}

public struct GrokBillingResponse: Decodable, Sendable {
    public var config: Config?

    public struct Config: Decodable, Sendable {
        public var creditUsagePercent: FlexibleNumber?
        public var currentPeriod: Period?
        public var monthlyLimit: Cents?
        public var used: Cents?
        public var billingPeriodEnd: FlexibleDate?
    }

    public struct Period: Decodable, Sendable {
        public var type: String?
        public var end: FlexibleDate?
    }

    public struct Cents: Decodable, Sendable {
        public var val: FlexibleNumber?
    }
}

public struct FlexibleNumber: Decodable, Sendable, Equatable {
    public var value: Double?

    public init(_ value: Double?) {
        self.value = value
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        value = if let number = try? container.decode(Double.self) {
            number
        } else if let string = try? container.decode(String.self) {
            Double(string.trimmingCharacters(in: .whitespaces))
        } else {
            nil
        }
    }
}

public struct FlexibleDate: Decodable, Sendable, Equatable {
    public var rawValue: String?
    public var number: Double?

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        rawValue = try? container.decode(String.self)
        number = try? container.decode(Double.self)
    }
}
