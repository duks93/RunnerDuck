/*
 KeychainClient.swift
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

public struct KeychainClient: DependencyClient {
    public var genericPassword: @Sendable (String) throws -> Data

    public static let liveValue = Self(
        genericPassword: { service in
            let process = Process()
            let pipe = Pipe()
            process.executableURL = URL(filePath: "/usr/bin/security")
            process.arguments = ["find-generic-password", "-s", service, "-w"]
            process.standardOutput = pipe
            process.standardError = FileHandle.nullDevice
            try process.run()
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            guard process.terminationStatus == .zero else {
                throw CocoaError(.fileReadNoPermission)
            }
            return data
        }
    )

    public static let testValue = Self(
        genericPassword: { _ in throw CocoaError(.fileReadNoSuchFile) }
    )
}
