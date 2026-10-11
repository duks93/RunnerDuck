/*
 AppUpdateService.swift
 Model

 Created by duks93 on 2026/10/11.
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

public struct AppUpdateService: Sendable {
    private let httpClient: HTTPClient

    public let installedCommit: String?

    public init(_ appDependencies: AppDependencies) {
        self.httpClient = appDependencies.httpClient
        let commit = appDependencies.buildInfoClient.commit()
        self.installedCommit = commit.flatMap { Self.isCommit($0) ? $0.lowercased() : nil }
    }

    public var changesURL: URL? {
        URL(string: "https://github.com/duks93/RunnerDuck/releases")
    }

    public func check() async -> AppUpdateStatus {
        guard let installedCommit else { return .unknownBuild }
        let url = URL(string: "https://github.com/duks93/RunnerDuck/releases/latest/download/latest.json")!
        var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 15)
        request.setValue("RunnerDuck", forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        do {
            let (data, response) = try await httpClient.data(request)
            guard let response = response as? HTTPURLResponse, response.statusCode == 200 else {
                return .failed
            }
            let manifest = try JSONDecoder().decode(Manifest.self, from: data)
            guard manifest.schemaVersion == 1,
                  Self.isCommit(manifest.commit),
                  manifest.commit == manifest.commit.lowercased(),
                  manifest.ancestors.allSatisfy({ Self.isCommit($0) && $0 == $0.lowercased() }),
                  !manifest.ancestors.contains(manifest.commit) else { return .failed }
            if manifest.commit == installedCommit { return .upToDate }
            return manifest.ancestors.contains(installedCommit) ? .available : .unpublishedBuild
        } catch {
            return .failed
        }
    }

    private static func isCommit(_ value: String) -> Bool {
        value.utf8.count == 40 && value.utf8.allSatisfy {
            (48...57).contains($0) || (65...70).contains($0) || (97...102).contains($0)
        }
    }

    private struct Manifest: Decodable {
        let schemaVersion: Int
        let commit: String
        let ancestors: [String]
    }
}
