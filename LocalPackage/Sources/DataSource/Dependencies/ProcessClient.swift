/*
 ProcessClient.swift
 DataSource

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

import Foundation

public struct ProcessClient: DependencyClient {
    public var run: @Sendable (URL, [String], URL) async throws -> Int32

    public static let liveValue = Self(
        run: { executableURL, arguments, logURL in
            let process = Process()
            let log = try FileHandle(forWritingTo: logURL)
            try log.seekToEnd()
            process.executableURL = executableURL
            process.arguments = arguments
            process.standardInput = FileHandle.nullDevice
            process.standardOutput = log
            process.standardError = log
            return try await withCheckedThrowingContinuation { continuation in
                process.terminationHandler = { continuation.resume(returning: $0.terminationStatus) }
                do {
                    try process.run()
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }
    )

    public static let testValue = Self(
        run: { _, _, _ in throw CocoaError(.executableNotLoadable) }
    )
}
