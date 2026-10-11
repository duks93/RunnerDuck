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
    private let dataClient: DataClient
    private let fileManagerClient: FileManagerClient
    private let httpClient: HTTPClient
    private let processClient: ProcessClient

    public let installedCommit: String?

    public init(_ appDependencies: AppDependencies) {
        self.dataClient = appDependencies.dataClient
        self.fileManagerClient = appDependencies.fileManagerClient
        self.httpClient = appDependencies.httpClient
        self.processClient = appDependencies.processClient
        let commit = appDependencies.buildInfoClient.commit()
        self.installedCommit = commit.flatMap { Self.isCommit($0) ? $0.lowercased() : nil }
    }

    public var changesURL: URL? {
        URL(string: "https://github.com/duks93/RunnerDuck/releases")
    }

    public var updateDirectory: URL? {
        fileManagerClient.urls(.cachesDirectory, .userDomainMask).first?
            .appending(path: "RunnerDuck/Update", directoryHint: .isDirectory)
    }

    public var updateLogURL: URL? {
        updateDirectory?.appending(path: "update.log", directoryHint: .notDirectory)
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
            return manifest.ancestors.contains(installedCommit) ? .available(manifest.commit) : .unpublishedBuild
        } catch {
            return .failed
        }
    }

    public func installUpdate(commit: String) async -> Bool {
        guard Self.isCommit(commit), commit == commit.lowercased(),
              let updateDirectory, let updateLogURL else {
            return false
        }
        let archiveURL = updateDirectory.appending(path: "source.tar.gz", directoryHint: .notDirectory)
        let sourceDirectory = updateDirectory.appending(path: "RunnerDuck-\(commit)", directoryHint: .isDirectory)
        var request = URLRequest(
            url: URL(string: "https://github.com/duks93/RunnerDuck/archive/\(commit).tar.gz")!,
            cachePolicy: .reloadIgnoringLocalCacheData,
            timeoutInterval: 60
        )
        request.setValue("RunnerDuck", forHTTPHeaderField: "User-Agent")
        do {
            if fileManagerClient.fileExists(updateDirectory.path(percentEncoded: false)) {
                try fileManagerClient.removeItem(updateDirectory)
            }
            try fileManagerClient.createDirectory(updateDirectory, true)
            try dataClient.write(Data(), updateLogURL)
            let (data, response) = try await httpClient.data(request)
            guard let response = response as? HTTPURLResponse, response.statusCode == 200 else {
                try dataClient.write(Data("Downloading \(request.url!.absoluteString) failed.\n".utf8), updateLogURL)
                return false
            }
            try dataClient.write(data, archiveURL)
            let extractionStatus = try await processClient.run(
                URL(filePath: "/usr/bin/tar"),
                ["-xzf", archiveURL.path(percentEncoded: false), "-C", updateDirectory.path(percentEncoded: false)],
                updateLogURL
            )
            try fileManagerClient.removeItem(archiveURL)
            guard extractionStatus == .zero else { return false }
            let installationStatus = try await processClient.run(
                URL(filePath: "/bin/bash"),
                ["-c", Self.installScript, "runnerduck-update", sourceDirectory.path(percentEncoded: false), commit],
                updateLogURL
            )
            return installationStatus == .zero
        } catch {
            try? dataClient.write(Data("\(error)\n".utf8), updateLogURL)
            return false
        }
    }

    // pgrep and pkill skip their ancestors, so install.sh cannot close this app; the wrapper restarts it instead.
    private static let installScript = #"""
        app_pid=$PPID
        RUNNERDUCK_SOURCE_COMMIT="$2" /bin/bash "$1/install.sh" --no-launch || exit
        rm -rf "$1"
        kill "$app_pid"
        for _ in {1..50}; do kill -0 "$app_pid" 2>/dev/null || break; sleep 0.2; done
        open "$HOME/Applications/RunnerDuck.app"
        """#

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
