/*
 AIUsageService.swift
 Model

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

import CryptoKit
import DataSource
import Foundation

struct AIUsageService {
    static let refreshInterval = Duration.seconds(300)
    static let claudeCodeKeychainService = "Claude Code-credentials"
    static let claudeCodeUserAgent = "claude-cli/2.1.280 (external, cli)"
    static let grokAuthKeyPrefix = "https://auth.x.ai::"
    static let grokClientVersion = "0.2.93"
    static let openCodexDefaultPort = 10100

    private let appStateClient: AppStateClient
    private let dataClient: DataClient
    private let dateClient: DateClient
    private let fileManagerClient: FileManagerClient
    private let httpClient: HTTPClient
    private let keychainClient: KeychainClient
    private let aiUsageWidgetService: AIUsageWidgetService
    private let logService: LogService
    private let userDefaultsRepository: UserDefaultsRepository

    init(_ appDependencies: AppDependencies) {
        self.appStateClient = appDependencies.appStateClient
        self.dataClient = appDependencies.dataClient
        self.dateClient = appDependencies.dateClient
        self.fileManagerClient = appDependencies.fileManagerClient
        self.httpClient = appDependencies.httpClient
        self.keychainClient = appDependencies.keychainClient
        self.aiUsageWidgetService = .init(appDependencies)
        self.logService = .init(appDependencies)
        self.userDefaultsRepository = .init(appDependencies.userDefaultsClient)
    }

    func startMonitoring() {
        guard appStateClient.withLock(\.aiUsageObserver) == nil else { return }
        let task = Task { [appStateClient] in
            await withTaskGroup { group in
                group.addTask {
                    while !Task.isCancelled {
                        await refresh()
                        try? await Task.sleep(for: Self.refreshInterval)
                    }
                }
                group.addTask {
                    let stream = appStateClient.withLock(\.aiUsageConfigurationChanges.stream)
                    for await _ in stream {
                        await refresh()
                    }
                }
            }
        }
        appStateClient.withLock {
            $0.aiUsageObserver = task
        }
    }

    func stopMonitoring() {
        appStateClient.withLock {
            $0.aiUsageRefreshGeneration &+= 1
            $0.aiUsageObserver?.cancel()
            $0.aiUsageObserver = nil
        }
    }

    func sourceStates() -> [AIUsageSourceState] {
        sourceStates(configuration: userDefaultsRepository.aiUsageConfiguration)
    }

    private func sourceStates(configuration: AIUsageConfiguration) -> [AIUsageSourceState] {
        AIUsageSource.allCases.map { source in
            let isDetected = isDetected(source)
            return AIUsageSourceState(
                source: source,
                isDetected: isDetected,
                isEnabled: configuration.isEnabled(source, isDetected: isDetected)
            )
        }
    }

    func setSource(_ source: AIUsageSource, isEnabled: Bool) {
        var configuration = userDefaultsRepository.aiUsageConfiguration
        configuration.set(isEnabled, for: source)
        userDefaultsRepository.aiUsageConfiguration = configuration
        appStateClient.withLock {
            $0.aiUsageRefreshGeneration &+= 1
        }
        appStateClient.send(\.aiUsageConfigurationChanges, ())
    }

    func refresh() async {
        guard !Task.isCancelled else { return }
        let (generation, configuration) = appStateClient.withLock {
            $0.aiUsageRefreshGeneration &+= 1
            return ($0.aiUsageRefreshGeneration, userDefaultsRepository.aiUsageConfiguration)
        }
        let enabledSources = sourceStates(configuration: configuration).filter(\.isEnabled).map(\.source)
        let bundles = await withTaskGroup(of: [AIUsageBundle].self) { group in
            enabledSources.forEach { source in
                group.addTask {
                    guard let provider = source.provider else {
                        return await fetchOpenCodexUsage(excludingLoginsOf: Set(enabledSources.compactMap(\.provider)))
                    }
                    return await fetchUsage(of: provider).map { [$0] } ?? []
                }
            }
            return await group.reduce(into: []) { $0 += $1 }
        }
        let sortedBundles = Self.sorted(bundles)
        let published = appStateClient.withLock { appState in
            guard !Task.isCancelled, appState.aiUsageRefreshGeneration == generation else { return false }
            var metrics = appState.metrics.latestValue ?? .init()
            metrics.aiUsageBundles = sortedBundles
            appState.metrics.send(metrics)
            aiUsageWidgetService.publish(sortedBundles)
            return true
        }
        guard published else { return }
        logService.notice(.aiUsageRefreshed(
            sources: enabledSources.map(\.rawValue),
            rows: sortedBundles.map { "\($0.id) \($0.status.logDescription)" }
        ))
    }

    func fetchOpenCodexUsage(excludingLoginsOf localProviders: Set<AIUsageProvider> = []) async -> [AIUsageBundle] {
        let baseURL = openCodexBaseURL()
        let token = openCodexAdminToken()
        let localIdentityHashes = localIdentityHashes(of: localProviders)
        let requests: [(AIUsageProvider, URL)] = [
            (.claudeCode, baseURL.appending(path: "api/oauth/accounts").appending(queryItems: [
                URLQueryItem(name: "provider", value: "anthropic"),
                URLQueryItem(name: "quota", value: "1"),
            ])),
            (.codex, baseURL.appending(path: "api/codex-auth/accounts")),
            (.grok, baseURL.appending(path: "api/oauth/accounts").appending(queryItems: [
                URLQueryItem(name: "provider", value: "xai"),
                URLQueryItem(name: "quota", value: "1"),
            ])),
        ]
        let bundles = await withTaskGroup(of: [AIUsageBundle].self) { group in
            requests.forEach { provider, url in
                group.addTask { await fetchOpenCodexAccounts(of: provider, from: url, token: token) }
            }
            return await group.reduce(into: []) { $0 += $1 }
        }
        return bundles.filter { bundle in
            guard let identityHash = localIdentityHashes[bundle.provider],
                  let accountID = bundle.accountID?.split(separator: "/").last else {
                return true
            }
            return !Self.isSameIdentity(openCodexAccountID: String(accountID), identityHash: identityHash)
        }
    }

    static func isSameIdentity(openCodexAccountID: String, identityHash: String) -> Bool {
        let baseID = openCodexAccountID.split(separator: "-").first.map(String.init) ?? openCodexAccountID
        return baseID.count >= 8 && identityHash.hasPrefix(baseID.lowercased())
    }

    static func identityHash(of identity: String) -> String {
        SHA256.hash(data: Data(identity.utf8)).map { String(format: "%02x", $0) }.joined()
    }

    func fetchUsage(of provider: AIUsageProvider) async -> AIUsageBundle? {
        let status: AIUsageBundle.Status
        do {
            let windows = switch provider {
            case .claudeCode: try await fetchClaudeCodeWindows()
            case .codex: try await fetchCodexWindows()
            case .grok: try await fetchGrokWindows()
            }
            status = .available(windows)
        } catch UsageError.credentialsNotFound {
            return nil
        } catch UsageError.unauthorized {
            status = .signInRequired
        } catch {
            logService.error(.fetchingAIUsageFailed(provider.rawValue, error))
            status = .failed
        }
        return AIUsageBundle(provider: provider, status: status, lastUpdatedDate: dateClient.now())
    }

    static func sorted(_ bundles: [AIUsageBundle]) -> [AIUsageBundle] {
        AIUsageProvider.allCases.flatMap { provider in
            let providerBundles = bundles.filter { $0.provider == provider }
            let localBundles = providerBundles.filter { $0.accountID == nil }
            let accountBundles = providerBundles
                .filter { $0.accountID != nil }
                .sorted { ($0.accountName ?? "") < ($1.accountName ?? "") }
            return localBundles + accountBundles
        }
    }

    static func bundle(
        from account: OpenCodexAccountsResponse.Account,
        of provider: AIUsageProvider,
        lastUpdatedDate: Date
    ) -> AIUsageBundle {
        let windows = account.quota.map { Self.windows(from: $0) } ?? []
        let status: AIUsageBundle.Status = if account.needsReauth == true {
            .signInRequired
        } else if windows.isEmpty {
            .failed
        } else {
            .available(windows)
        }
        return AIUsageBundle(
            provider: provider,
            accountID: "openCodex/\(account.id)",
            accountName: account.alias ?? account.email ?? "opencodex",
            status: status,
            lastUpdatedDate: lastUpdatedDate
        )
    }

    static func windows(from quota: OpenCodexAccountsResponse.Quota) -> [AIUsageWindow] {
        func window(_ kind: AIUsageWindow.Kind, _ percent: FlexibleNumber?, _ resetAt: FlexibleNumber?) -> AIUsageWindow? {
            guard let percentage = normalizedPercentage(percent?.value) else { return nil }
            return AIUsageWindow(kind: kind, percentage: percentage, resetDate: date(fromEpoch: resetAt?.value))
        }
        let fiveHourPercent = quota.fiveHourPercent?.value == nil ? quota.shortPercent : quota.fiveHourPercent
        let fiveHourResetAt = quota.fiveHourResetAt?.value == nil ? quota.shortResetAt : quota.fiveHourResetAt
        let standardWindows = [
            window(.fiveHour, fiveHourPercent, fiveHourResetAt),
            window(.weekly, quota.weeklyPercent, quota.weeklyResetAt),
            window(.monthly, quota.monthlyPercent, quota.monthlyResetAt),
        ]
        let customWindows = (quota.customWindows ?? []).map {
            window(.weeklyModel($0.label), $0.percent, $0.resetAt)
        }
        return (standardWindows + customWindows).compactMap(\.self)
    }

    static func windows(from response: ClaudeCodeUsageResponse) -> [AIUsageWindow] {
        let buckets: [(AIUsageWindow.Kind, ClaudeCodeUsageResponse.Bucket?)] = [
            (.fiveHour, response.fiveHour),
            (.weekly, response.sevenDay),
            (.weeklyModel("Fable"), response.sevenDayFable),
            (.weeklyModel("Opus"), response.sevenDayOpus),
            (.weeklyModel("Sonnet"), response.sevenDaySonnet),
        ]
        let bucketWindows = buckets.compactMap { kind, bucket -> AIUsageWindow? in
            guard let percentage = normalizedPercentage(bucket?.utilization?.value) else { return nil }
            return AIUsageWindow(kind: kind, percentage: percentage, resetDate: date(from: bucket?.resetsAt))
        }
        let scopedWindows = (response.limits ?? []).compactMap { limit -> AIUsageWindow? in
            guard limit.kind?.lowercased() == "weekly_scoped",
                  let label = modelLabel(from: limit.scope?.model?.displayName),
                  let percentage = normalizedPercentage(limit.percent?.value) else {
                return nil
            }
            return AIUsageWindow(kind: .weeklyModel(label), percentage: percentage, resetDate: date(from: limit.resetsAt))
        }
        return (bucketWindows + scopedWindows).reduce(into: []) { windows, window in
            guard !windows.contains(where: { $0.kind == window.kind }) else { return }
            windows.append(window)
        }
    }

    static func windows(from response: CodexUsageResponse) -> [AIUsageWindow] {
        guard let rateLimit = response.rateLimit else { return [] }
        let rawWindows = [rateLimit.primaryWindow, rateLimit.secondaryWindow, rateLimit.tertiaryWindow]
        return rawWindows.compactMap(\.self).reduce(into: []) { windows, rawWindow in
            let kind = kind(ofWindowSeconds: rawWindow.limitWindowSeconds?.value)
            guard let percentage = normalizedPercentage(rawWindow.usedPercent?.value),
                  !windows.contains(where: { $0.kind == kind }) else {
                return
            }
            windows.append(AIUsageWindow(
                kind: kind,
                percentage: percentage,
                resetDate: date(fromEpoch: rawWindow.resetAt?.value)
            ))
        }
    }

    static func weeklyCreditWindow(from response: GrokBillingResponse) -> AIUsageWindow? {
        guard let config = response.config,
              config.currentPeriod?.type == "USAGE_PERIOD_TYPE_WEEKLY",
              let percentage = normalizedPercentage(config.creditUsagePercent?.value ?? .zero) else {
            return nil
        }
        return AIUsageWindow(kind: .weekly, percentage: percentage, resetDate: date(from: config.currentPeriod?.end))
    }

    static func monthlyBillingWindow(from response: GrokBillingResponse) -> AIUsageWindow? {
        guard let config = response.config,
              let limit = config.monthlyLimit?.val?.value, limit > .zero,
              let used = config.used?.val?.value,
              let percentage = normalizedPercentage(used / limit * 100) else {
            return nil
        }
        return AIUsageWindow(kind: .monthly, percentage: percentage, resetDate: date(from: config.billingPeriodEnd))
    }

    static func kind(ofWindowSeconds seconds: Double?) -> AIUsageWindow.Kind {
        guard let seconds, seconds > .zero else { return .weekly }
        return switch seconds {
        case ..<86_400: .fiveHour
        case 2_419_200...: .monthly
        default: .weekly
        }
    }

    static func normalizedPercentage(_ value: Double?) -> Double? {
        guard let value, value.isFinite else { return nil }
        return max(0, min(100, value))
    }

    static func date(fromEpoch value: Double?) -> Date? {
        guard let value, value.isFinite, value > .zero else { return nil }
        return Date(timeIntervalSince1970: value > 10_000_000_000 ? value / 1000 : value)
    }

    static func date(from string: String?) -> Date? {
        guard let string = string?.trimmingCharacters(in: .whitespaces), !string.isEmpty else { return nil }
        if let number = Double(string) {
            return date(fromEpoch: number)
        }
        return try? Date.ISO8601FormatStyle().parse(string.replacing(/\.\d+/, with: ""))
    }

    static func date(from flexibleDate: FlexibleDate?) -> Date? {
        date(from: flexibleDate?.rawValue) ?? date(fromEpoch: flexibleDate?.number)
    }

    private static func modelLabel(from displayName: String?) -> String? {
        let lowercased = displayName?.lowercased() ?? ""
        return ["Fable", "Opus", "Sonnet"].first { lowercased.contains($0.lowercased()) }
    }

    private var homeDirectory: URL {
        fileManagerClient.homeDirectoryForCurrentUser()
    }

    private var openCodexDirectory: URL {
        homeDirectory.appending(path: ".opencodex")
    }

    private func isDetected(_ source: AIUsageSource) -> Bool {
        switch source {
        case .claudeCode: (try? loadClaudeCodeCredentials()) != nil
        case .codex: (try? loadCodexTokens()) != nil
        case .grok: (try? loadGrokAuthEntry()) != nil
        case .openCodex: fileManagerClient.fileExists(openCodexDirectory.path(percentEncoded: false))
        }
    }

    private func localIdentityHashes(of providers: Set<AIUsageProvider>) -> [AIUsageProvider: String] {
        guard providers.contains(.grok), let userID = try? loadGrokAuthEntry().userID else { return [:] }
        return [.grok: Self.identityHash(of: userID)]
    }

    private func openCodexBaseURL() -> URL {
        let fileURL = openCodexDirectory.appending(path: "runtime-port.json")
        let savedPort = (try? dataClient.read(fileURL))
            .flatMap { try? JSONDecoder().decode(OpenCodexRuntimePort.self, from: $0).port }
        let port = if let savedPort, (1024 ... 65535).contains(savedPort) {
            savedPort
        } else {
            Self.openCodexDefaultPort
        }
        return URL(string: "http://127.0.0.1:\(port)")!
    }

    private func openCodexAdminToken() -> String? {
        let fileURL = openCodexDirectory.appending(path: "admin-api-token")
        guard let data = try? dataClient.read(fileURL) else { return nil }
        let token = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
        return token.isEmpty ? nil : token
    }

    private func fetchOpenCodexAccounts(of provider: AIUsageProvider, from url: URL, token: String?) async -> [AIUsageBundle] {
        var request = URLRequest(url: url, timeoutInterval: 10)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let token {
            request.setValue(token, forHTTPHeaderField: "x-opencodex-api-key")
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        guard let data = try? await send(request),
              let response = try? JSONDecoder().decode(OpenCodexAccountsResponse.self, from: data) else {
            return []
        }
        let now = dateClient.now()
        return (response.accounts ?? [])
            .filter { $0.isMain != true }
            .map { Self.bundle(from: $0, of: provider, lastUpdatedDate: now) }
    }

    private func fetchClaudeCodeWindows() async throws -> [AIUsageWindow] {
        let oauth = try loadClaudeCodeCredentials().claudeAiOauth
        if let expiresAt = oauth.expiresAt, Date(timeIntervalSince1970: expiresAt / 1000) < dateClient.now() {
            throw UsageError.unauthorized
        }
        var request = URLRequest(url: .claudeCodeUsage, timeoutInterval: 10)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(oauth.accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue("oauth-2025-04-20", forHTTPHeaderField: "anthropic-beta")
        request.setValue(Self.claudeCodeUserAgent, forHTTPHeaderField: "User-Agent")
        let response = try decode(ClaudeCodeUsageResponse.self, from: try await send(request))
        return try nonEmpty(Self.windows(from: response))
    }

    private func loadClaudeCodeCredentials() throws -> ClaudeCodeCredentials {
        let fileURL = homeDirectory.appending(path: ".claude/.credentials.json")
        let data = (try? keychainClient.genericPassword(Self.claudeCodeKeychainService)) ?? (try? dataClient.read(fileURL))
        guard let data, let credentials = try? JSONDecoder().decode(ClaudeCodeCredentials.self, from: data) else {
            throw UsageError.credentialsNotFound
        }
        return credentials
    }

    private func loadCodexTokens() throws -> CodexAuth.Tokens {
        let fileURL = homeDirectory.appending(path: ".codex/auth.json")
        guard let data = try? dataClient.read(fileURL),
              let tokens = try? JSONDecoder().decode(CodexAuth.self, from: data).tokens else {
            throw UsageError.credentialsNotFound
        }
        return tokens
    }

    private func fetchCodexWindows() async throws -> [AIUsageWindow] {
        let tokens = try loadCodexTokens()
        var request = URLRequest(url: .codexUsage, timeoutInterval: 10)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(tokens.accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue(tokens.accountID, forHTTPHeaderField: "ChatGPT-Account-Id")
        let response = try decode(CodexUsageResponse.self, from: try await send(request))
        return try nonEmpty(Self.windows(from: response))
    }

    private func fetchGrokWindows() async throws -> [AIUsageWindow] {
        let entry = try loadGrokAuthEntry()
        if let userID = entry.userID {
            do {
                let response = try await fetchGrokBilling(from: .grokCredits, entry: entry, userID: userID)
                if let window = Self.weeklyCreditWindow(from: response) {
                    return [window]
                }
                logService.error(.fetchingAIUsageFailed("grokCredits", UsageError.unexpectedResponse(statusCode: 200)))
            } catch {
                logService.error(.fetchingAIUsageFailed("grokCredits", error))
            }
        }
        let response = try await fetchGrokBilling(from: .grokBilling, entry: entry, userID: nil)
        guard let window = Self.monthlyBillingWindow(from: response) else {
            throw UsageError.unexpectedResponse(statusCode: 200)
        }
        return [window]
    }

    private func loadGrokAuthEntry() throws -> GrokAuthEntry {
        let fileURL = homeDirectory.appending(path: ".grok/auth.json")
        guard let data = try? dataClient.read(fileURL),
              let entries = try? JSONDecoder().decode([String: GrokAuthEntry].self, from: data),
              let entry = entries.first(where: { $0.key.hasPrefix(Self.grokAuthKeyPrefix) })?.value else {
            throw UsageError.credentialsNotFound
        }
        return entry
    }

    private func fetchGrokBilling(from url: URL, entry: GrokAuthEntry, userID: String?) async throws -> GrokBillingResponse {
        var request = URLRequest(url: url, timeoutInterval: 10)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(entry.key)", forHTTPHeaderField: "Authorization")
        if let userID {
            request.setValue("xai-grok-cli", forHTTPHeaderField: "x-xai-token-auth")
            request.setValue("authenticate-response", forHTTPHeaderField: "x-authenticateresponse")
            request.setValue(userID, forHTTPHeaderField: "x-userid")
            request.setValue(Self.grokClientVersion, forHTTPHeaderField: "x-grok-client-version")
        }
        return try decode(GrokBillingResponse.self, from: try await send(request))
    }

    private func send(_ request: URLRequest) async throws -> Data {
        let (data, response) = try await httpClient.data(request)
        guard let statusCode = (response as? HTTPURLResponse)?.statusCode else {
            throw UsageError.unexpectedResponse(statusCode: nil)
        }
        return switch statusCode {
        case 200 ..< 300: data
        case 401, 403: throw UsageError.unauthorized
        default: throw UsageError.unexpectedResponse(statusCode: statusCode)
        }
    }

    private func decode<T: Decodable>(_ type: T.Type, from data: Data) throws -> T {
        do {
            return try JSONDecoder().decode(type, from: data)
        } catch {
            throw UsageError.decodingFailed(String(describing: type))
        }
    }

    private func nonEmpty(_ windows: [AIUsageWindow]) throws -> [AIUsageWindow] {
        guard !windows.isEmpty else {
            throw UsageError.noWindows
        }
        return windows
    }

    enum UsageError: Error {
        case credentialsNotFound
        case unauthorized
        case unexpectedResponse(statusCode: Int?)
        case decodingFailed(String)
        case noWindows
    }
}

private extension AIUsageBundle.Status {
    var logDescription: String {
        switch self {
        case let .available(windows): "available(\(windows.count))"
        case .signInRequired: "signInRequired"
        case .failed: "failed"
        }
    }
}
