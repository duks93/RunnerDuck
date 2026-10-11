/*
 GeneralSettings.swift
 Model

 Created by Takuto Nakamura on 2026/05/23.
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
import Observation

@MainActor @Observable
public final class GeneralSettings: Composable {
    private let appUpdateService: AppUpdateService
    private let nsWorkspaceClient: NSWorkspaceClient
    private let launchAtLoginRepository: LaunchAtLoginRepository
    private let userDefaultsRepository: UserDefaultsRepository
    private let logService: LogService
    private let systemMetricsService: SystemMetricsService

    public private(set) var appUpdateStatus = AppUpdateStatus.notChecked
    public var installedCommit: String? { appUpdateService.installedCommit }
    public var canCheckForUpdates: Bool { appUpdateStatus != .checking && appUpdateStatus != .installing }
    public var isUpdateAvailable: Bool { availableCommit != nil }
    public var isInstallingUpdate: Bool { appUpdateStatus == .installing }
    public var showsUpdateLog: Bool { appUpdateStatus == .installFailed }
    public var updateInterval: UpdateInterval
    public var launchesAtLogin: Bool
    public let action: (Action) async -> Void

    public init(
        _ appDependencies: AppDependencies,
        updateInterval: UpdateInterval? = nil,
        launchesAtLogin: Bool? = nil,
        action: @escaping (Action) async -> Void = { _ in }
    ) {
        self.appUpdateService = .init(appDependencies)
        self.nsWorkspaceClient = appDependencies.nsWorkspaceClient
        self.launchAtLoginRepository = .init(appDependencies.smAppServiceClient)
        self.userDefaultsRepository = .init(appDependencies.userDefaultsClient)
        self.logService = .init(appDependencies)
        self.systemMetricsService = .init(appDependencies)
        self.updateInterval = updateInterval ?? userDefaultsRepository.updateInterval
        self.launchesAtLogin = launchesAtLogin ?? launchAtLoginRepository.isEnabled
        self.action = action
    }

    public func reduce(_ action: Action) async {
        switch action {
        case let .viewAppeared(screenName):
            logService.notice(.screenView(name: screenName))
            launchesAtLogin = launchAtLoginRepository.isEnabled

        case .checkForUpdates:
            guard canCheckForUpdates else { return }
            appUpdateStatus = .checking
            appUpdateStatus = await appUpdateService.check()

        case .installUpdate:
            guard let availableCommit else { return }
            appUpdateStatus = .installing
            appUpdateStatus = await appUpdateService.installUpdate(commit: availableCommit) ? .upToDate : .installFailed

        case .showUpdateLog:
            guard let updateLogURL = appUpdateService.updateLogURL else { return }
            nsWorkspaceClient.activateFileViewerSelecting([updateLogURL])

        case .openUpdateChanges:
            guard let url = appUpdateService.changesURL else { return }
            _ = nsWorkspaceClient.open(url)

        case let .updateIntervalPickerSelected(interval):
            updateInterval = interval
            userDefaultsRepository.updateInterval = interval
            systemMetricsService.stopMonitoring()
            systemMetricsService.startMonitoring()

        case let .launchAtLoginToggleSwitched(isOn):
            userDefaultsRepository.launchAtLoginInitialized = true
            switch launchAtLoginRepository.switchStatus(isOn) {
            case .success:
                launchesAtLogin = isOn
            case let .failure(.switchFailed(value)):
                launchesAtLogin = value
            }
        }
    }

    private var availableCommit: String? {
        guard case let .available(commit) = appUpdateStatus else { return nil }
        return commit
    }

    public enum Action: Sendable {
        case viewAppeared(String)
        case checkForUpdates
        case installUpdate
        case showUpdateLog
        case openUpdateChanges
        case updateIntervalPickerSelected(UpdateInterval)
        case launchAtLoginToggleSwitched(Bool)
    }
}
