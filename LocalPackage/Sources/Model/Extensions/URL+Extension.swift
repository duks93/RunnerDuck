/*
 URL+Extension.swift
 Model

 Created by Takuto Nakamura on 2026/06/09.
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

extension URL {
    public static let github = URL(
        string: "https://github.com/duks93/RunnerDuck"
    )!

    public static var githubIssues: URL {
        Self.github.appending(path: "issues")
    }

    public static var customMetricsSchema: URL {
        Self.github.appending(path: "blob/main/docs/CustomMetricsSchema.md")
    }

    public static let runnerGuide = Self.github.appending(path: "blob/main/docs/CustomRunners.md")

    static let claudeCodeUsage = URL(
        string: "https://api.anthropic.com/api/oauth/usage"
    )!

    static let codexUsage = URL(
        string: "https://chatgpt.com/backend-api/wham/usage"
    )!

    static let grokBilling = URL(
        string: "https://cli-chat-proxy.grok.com/v1/billing"
    )!

    static var grokCredits: URL {
        Self.grokBilling.appending(queryItems: [URLQueryItem(name: "format", value: "credits")])
    }
}
