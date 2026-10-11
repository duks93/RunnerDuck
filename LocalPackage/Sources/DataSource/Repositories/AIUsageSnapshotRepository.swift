/*
 AIUsageSnapshotRepository.swift
 DataSource

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


import Foundation
import UniformTypeIdentifiers

public struct AIUsageSnapshotRepository: Sendable {
    private var dataClient: DataClient
    private var fileManagerClient: FileManagerClient

    private var directoryURL: URL? {
        fileManagerClient.realHomeDirectory()?
            .appending(path: "Library/Application Support/RunnerDuck", directoryHint: .isDirectory)
    }

    private var fileURL: URL? {
        directoryURL?
            .appending(path: String.aiUsageSnapshot)
            .appendingPathExtension(for: .json)
    }

    public init(
        _ dataClient: DataClient,
        _ fileManagerClient: FileManagerClient
    ) {
        self.dataClient = dataClient
        self.fileManagerClient = fileManagerClient
    }

    public func loadBundles() -> [AIUsageBundle] {
        guard let fileURL,
              let data = try? dataClient.read(fileURL),
              let bundles = try? JSONDecoder().decode([AIUsageBundle].self, from: data) else {
            return []
        }
        return bundles
    }

    public func saveBundles(_ bundles: [AIUsageBundle]) throws {
        guard let directoryURL, let fileURL else {
            throw CocoaError(.fileNoSuchFile)
        }
        if !fileManagerClient.fileExists(directoryURL.path(percentEncoded: false)) {
            try fileManagerClient.createDirectory(directoryURL, true)
        }
        let data = try JSONEncoder().encode(bundles)
        try dataClient.write(data, fileURL)
    }
}
