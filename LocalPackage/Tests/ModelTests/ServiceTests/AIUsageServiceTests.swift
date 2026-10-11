import AllocatedUnfairLock
import Foundation
import Testing

@testable import DataSource
@testable import Model

struct AIUsageServiceTests {
    private let claudeCodeCredentialsJSON = """
        { "claudeAiOauth": { "accessToken": "claude-token", "expiresAt": 1790000000000 } }
        """

    private let codexAuthJSON = """
        { "tokens": { "access_token": "codex-token", "account_id": "account" } }
        """

    private let grokAuthJSON = """
        { "https://auth.x.ai::client": { "key": "grok-token", "user_id": "user" } }
        """

    private func date(_ string: String) throws -> Date {
        try #require(ISO8601DateFormatter().date(from: string))
    }

    private func response(_ statusCode: Int, _ body: String = "{}") -> (Data, URLResponse) {
        let url = URL(string: "https://example.com")!
        return (Data(body.utf8), HTTPURLResponse(url: url, statusCode: statusCode, httpVersion: nil, headerFields: nil)!)
    }

    private func fileDataClient(_ files: [String: String]) -> DataClient {
        testDependency(of: DataClient.self) {
            $0.read = { url in
                guard let body = files.first(where: { url.path().hasSuffix($0.key) })?.value else {
                    throw CocoaError(.fileReadNoSuchFile)
                }
                return Data(body.utf8)
            }
        }
    }

    @Test
    func fetchUsage_claudeCode_maps_buckets_and_scoped_limits_to_windows() async throws {
        let requests = AllocatedUnfairLock<[URLRequest]>(initialState: [])
        let sut = AIUsageService(.testDependencies(
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { request in
                    requests.withLock { $0.append(request) }
                    return response(200, """
                        {
                          "five_hour": { "utilization": 42.5, "resets_at": "2026-09-25T15:00:00.123456+00:00" },
                          "seven_day": { "utilization": 120, "resets_at": "2026-09-30T00:00:00Z" },
                          "seven_day_opus": null,
                          "seven_day_sonnet": { "utilization": null, "resets_at": null },
                          "limits": [
                            { "kind": "session", "percent": 42.5 },
                            { "kind": "weekly_scoped", "percent": 7, "scope": { "model": { "display_name": "Claude Sonnet 5" } } }
                          ]
                        }
                        """)
                }
            },
            keychainClient: testDependency(of: KeychainClient.self) { [claudeCodeCredentialsJSON] in
                $0.genericPassword = { _ in Data(claudeCodeCredentialsJSON.utf8) }
            }
        ))
        let bundle = await sut.fetchUsage(of: .claudeCode)
        #expect(bundle == AIUsageBundle(
            provider: .claudeCode,
            status: .available([
                AIUsageWindow(kind: .fiveHour, percentage: 42.5, resetDate: try date("2026-09-25T15:00:00Z")),
                AIUsageWindow(kind: .weekly, percentage: 100, resetDate: try date("2026-09-30T00:00:00Z")),
                AIUsageWindow(kind: .weeklyModel("Sonnet"), percentage: 7),
            ]),
            lastUpdatedDate: .distantPast
        ))
        let request = try #require(requests.withLock { $0.first })
        #expect(request.url == URL(string: "https://api.anthropic.com/api/oauth/usage"))
        #expect(request.value(forHTTPHeaderField: "Authorization") == "Bearer claude-token")
    }

    @Test
    func fetchUsage_claudeCode_falls_back_to_credentials_file_when_keychain_is_unavailable() async {
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".claude/.credentials.json": claudeCodeCredentialsJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { _ in response(200, #"{ "five_hour": { "utilization": 1 } }"#) }
            }
        ))
        let bundle = await sut.fetchUsage(of: .claudeCode)
        #expect(bundle?.status == .available([AIUsageWindow(kind: .fiveHour, percentage: 1)]))
    }

    @Test
    func fetchUsage_claudeCode_returns_signInRequired_without_request_when_token_expired() async throws {
        let requestCount = AllocatedUnfairLock(initialState: 0)
        let sut = AIUsageService(.testDependencies(
            dateClient: testDependency(of: DateClient.self) {
                $0.now = { Date(timeIntervalSince1970: 1_800_000_000) }
            },
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { _ in
                    requestCount.withLock { $0 += 1 }
                    return response(200)
                }
            },
            keychainClient: testDependency(of: KeychainClient.self) { [claudeCodeCredentialsJSON] in
                $0.genericPassword = { _ in Data(claudeCodeCredentialsJSON.utf8) }
            }
        ))
        let bundle = await sut.fetchUsage(of: .claudeCode)
        #expect(bundle?.status == .signInRequired)
        #expect(requestCount.withLock(\.self) == 0)
    }

    @Test(arguments: AIUsageProvider.allCases)
    func fetchUsage_returns_nil_when_credentials_are_missing(provider: AIUsageProvider) async {
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([:])
        ))
        #expect(await sut.fetchUsage(of: provider) == nil)
    }

    @Test
    func fetchUsage_codex_classifies_windows_by_declared_duration() async throws {
        let requests = AllocatedUnfairLock<[URLRequest]>(initialState: [])
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".codex/auth.json": codexAuthJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { request in
                    requests.withLock { $0.append(request) }
                    return response(200, """
                        {
                          "rate_limit": {
                            "primary_window": { "used_percent": 12, "reset_at": 1790000000, "limit_window_seconds": 18000 },
                            "secondary_window": { "used_percent": 34, "reset_at": 1790500000, "limit_window_seconds": 604800 },
                            "tertiary_window": null
                          }
                        }
                        """)
                }
            }
        ))
        let bundle = await sut.fetchUsage(of: .codex)
        #expect(bundle?.status == .available([
            AIUsageWindow(kind: .fiveHour, percentage: 12, resetDate: Date(timeIntervalSince1970: 1_790_000_000)),
            AIUsageWindow(kind: .weekly, percentage: 34, resetDate: Date(timeIntervalSince1970: 1_790_500_000)),
        ]))
        let request = try #require(requests.withLock { $0.first })
        #expect(request.url == URL(string: "https://chatgpt.com/backend-api/wham/usage"))
        #expect(request.value(forHTTPHeaderField: "Authorization") == "Bearer codex-token")
        #expect(request.value(forHTTPHeaderField: "ChatGPT-Account-Id") == "account")
    }

    @Test
    func fetchUsage_codex_returns_signInRequired_when_unauthorized() async {
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".codex/auth.json": codexAuthJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { _ in response(401) }
            }
        ))
        #expect(await sut.fetchUsage(of: .codex)?.status == .signInRequired)
    }

    @Test
    func fetchUsage_codex_returns_failed_when_server_errors() async {
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".codex/auth.json": codexAuthJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { _ in response(500) }
            }
        ))
        #expect(await sut.fetchUsage(of: .codex)?.status == .failed)
    }

    @Test
    func fetchUsage_grok_prefers_weekly_credits() async throws {
        let requests = AllocatedUnfairLock<[URLRequest]>(initialState: [])
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".grok/auth.json": grokAuthJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { request in
                    requests.withLock { $0.append(request) }
                    return response(200, """
                        {
                          "config": {
                            "creditUsagePercent": 55.5,
                            "currentPeriod": { "type": "USAGE_PERIOD_TYPE_WEEKLY", "end": "2026-09-28T00:00:00Z" }
                          }
                        }
                        """)
                }
            }
        ))
        let bundle = await sut.fetchUsage(of: .grok)
        #expect(bundle?.status == .available([
            AIUsageWindow(kind: .weekly, percentage: 55.5, resetDate: try date("2026-09-28T00:00:00Z")),
        ]))
        let request = try #require(requests.withLock { $0.first })
        #expect(request.url == URL(string: "https://cli-chat-proxy.grok.com/v1/billing?format=credits"))
        #expect(request.value(forHTTPHeaderField: "x-userid") == "user")
    }

    @Test
    func fetchUsage_grok_falls_back_to_monthly_billing_when_weekly_credits_are_unavailable() async throws {
        let sut = AIUsageService(.testDependencies(
            dataClient: fileDataClient([".grok/auth.json": grokAuthJSON]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { request in
                    guard request.url?.query() == nil else {
                        return response(404)
                    }
                    return response(200, """
                        {
                          "config": {
                            "monthlyLimit": { "val": "5000" },
                            "used": { "val": 1250 },
                            "billingPeriodEnd": "2026-10-01T00:00:00Z"
                          }
                        }
                        """)
                }
            }
        ))
        let bundle = await sut.fetchUsage(of: .grok)
        #expect(bundle?.status == .available([
            AIUsageWindow(kind: .monthly, percentage: 25, resetDate: try date("2026-10-01T00:00:00Z")),
        ]))
    }

    @Test
    func refresh_emits_available_bundles_in_provider_order() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        let sut = AIUsageService(.testDependencies(
            appStateClient: .testDependency(appState),
            dataClient: fileDataClient([
                ".codex/auth.json": codexAuthJSON,
                ".grok/auth.json": grokAuthJSON,
            ]),
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { _ in response(401) }
            }
        ))
        await sut.refresh()
        let bundles = appState.withLock(\.metrics.latestValue?.aiUsageBundles)
        #expect(bundles == [
            AIUsageBundle(provider: .codex, status: .signInRequired, lastUpdatedDate: .distantPast),
            AIUsageBundle(provider: .grok, status: .signInRequired, lastUpdatedDate: .distantPast),
        ])
    }

    @Test(arguments: [
        (Double?.none, AIUsageWindow.Kind.weekly),
        (18_000, .fiveHour),
        (604_800, .weekly),
        (2_592_000, .monthly),
    ])
    func kind_ofWindowSeconds_classifies_duration(seconds: Double?, expected: AIUsageWindow.Kind) {
        #expect(AIUsageService.kind(ofWindowSeconds: seconds) == expected)
    }
}

struct AIUsageServiceOpenCodexTests {
    private func response(_ statusCode: Int, _ body: String = "{}") -> (Data, URLResponse) {
        let url = URL(string: "https://example.com")!
        return (Data(body.utf8), HTTPURLResponse(url: url, statusCode: statusCode, httpVersion: nil, headerFields: nil)!)
    }

    @Test
    func fetchOpenCodexUsage_maps_pool_accounts_and_skips_main_account() async throws {
        let requests = AllocatedUnfairLock<[URLRequest]>(initialState: [])
        let sut = AIUsageService(.testDependencies(
            dataClient: testDependency(of: DataClient.self) {
                $0.read = { url in
                    switch url.lastPathComponent {
                    case "runtime-port.json": Data(#"{ "port": 10200 }"#.utf8)
                    case "admin-api-token": Data("ocx_admin_token\n".utf8)
                    default: throw CocoaError(.fileReadNoSuchFile)
                    }
                }
            },
            httpClient: testDependency(of: HTTPClient.self) { [self] in
                $0.data = { request in
                    requests.withLock { $0.append(request) }
                    guard request.url?.path() == "/api/codex-auth/accounts" else {
                        return response(200, #"{ "accounts": [] }"#)
                    }
                    return response(200, """
                        {
                          "accounts": [
                            { "id": "main", "email": "main@example.com", "isMain": true, "quota": { "weeklyPercent": 1 } },
                            { "id": "sub", "alias": "sub-1", "email": "sub@example.com",
                              "quota": { "shortPercent": 20, "shortResetAt": 1790000000, "weeklyPercent": 70 } },
                            { "id": "expired", "email": "expired@example.com", "needsReauth": true, "quota": null }
                          ]
                        }
                        """)
                }
            }
        ))
        let bundles = await sut.fetchOpenCodexUsage()
        #expect(AIUsageService.sorted(bundles) == [
            AIUsageBundle(
                provider: .codex,
                accountID: "openCodex/expired",
                accountName: "expired@example.com",
                status: .signInRequired,
                lastUpdatedDate: .distantPast
            ),
            AIUsageBundle(
                provider: .codex,
                accountID: "openCodex/sub",
                accountName: "sub-1",
                status: .available([
                    AIUsageWindow(kind: .fiveHour, percentage: 20, resetDate: Date(timeIntervalSince1970: 1_790_000_000)),
                    AIUsageWindow(kind: .weekly, percentage: 70),
                ]),
                lastUpdatedDate: .distantPast
            ),
        ])
        let recordedRequests = requests.withLock(\.self)
        #expect(recordedRequests.count == 3)
        #expect(recordedRequests.allSatisfy { $0.url?.host() == "127.0.0.1" && $0.url?.port == 10200 })
        #expect(recordedRequests.allSatisfy { $0.value(forHTTPHeaderField: "x-opencodex-api-key") == "ocx_admin_token" })
    }

    @Test
    func fetchOpenCodexUsage_returns_empty_when_proxy_is_not_running() async {
        let sut = AIUsageService(.testDependencies())
        #expect(await sut.fetchOpenCodexUsage().isEmpty)
    }

    @Test
    func windows_fromOpenCodexQuota_includes_model_scoped_windows() {
        let quota = OpenCodexAccountsResponse.Quota(
            fiveHourPercent: FlexibleNumber(10),
            fiveHourResetAt: FlexibleNumber(1_790_000_000_000),
            weeklyPercent: FlexibleNumber(30),
            customWindows: [.init(label: "Opus", percent: FlexibleNumber(50))]
        )
        #expect(AIUsageService.windows(from: quota) == [
            AIUsageWindow(kind: .fiveHour, percentage: 10, resetDate: Date(timeIntervalSince1970: 1_790_000_000)),
            AIUsageWindow(kind: .weekly, percentage: 30),
            AIUsageWindow(kind: .weeklyModel("Opus"), percentage: 50),
        ])
    }

    @Test
    func sourceStates_enables_detected_sources_unless_overridden() {
        let storage = UserDefaultsClient.storage()
        let sut = AIUsageService(.testDependencies(
            dataClient: testDependency(of: DataClient.self) {
                $0.read = { url in
                    guard url.path().hasSuffix(".codex/auth.json") else { throw CocoaError(.fileReadNoSuchFile) }
                    return Data(#"{ "tokens": { "access_token": "token" } }"#.utf8)
                }
            },
            fileManagerClient: testDependency(of: FileManagerClient.self) {
                $0.fileExists = { $0.hasSuffix(".opencodex") }
            },
            userDefaultsClient: storage.client
        ))
        #expect(sut.sourceStates() == [
            AIUsageSourceState(source: .claudeCode, isDetected: false, isEnabled: false),
            AIUsageSourceState(source: .codex, isDetected: true, isEnabled: true),
            AIUsageSourceState(source: .grok, isDetected: false, isEnabled: false),
            AIUsageSourceState(source: .openCodex, isDetected: true, isEnabled: true),
        ])
        sut.setSource(.codex, isEnabled: false)
        sut.setSource(.grok, isEnabled: true)
        #expect(sut.sourceStates().map(\.isEnabled) == [false, false, true, true])
    }

    @Test
    func refresh_skips_disabled_sources() async {
        let storage = UserDefaultsClient.storage()
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        let requestCount = AllocatedUnfairLock(initialState: 0)
        let sut = AIUsageService(.testDependencies(
            appStateClient: .testDependency(appState),
            dataClient: testDependency(of: DataClient.self) {
                $0.read = { _ in Data(#"{ "tokens": { "access_token": "token" } }"#.utf8) }
            },
            httpClient: testDependency(of: HTTPClient.self) {
                $0.data = { _ in
                    requestCount.withLock { $0 += 1 }
                    throw URLError(.cannotConnectToHost)
                }
            },
            userDefaultsClient: storage.client
        ))
        sut.setSource(.codex, isEnabled: false)
        await sut.refresh()
        #expect(requestCount.withLock(\.self) == 0)
        #expect(appState.withLock(\.metrics.latestValue?.aiUsageBundles) == [])
    }
}

struct AIUsageServiceIdentityTests {
    @Test
    func fetchOpenCodexUsage_hides_account_matching_local_grok_login() async {
        let grokAuthJSON = #"{ "https://auth.x.ai::client": { "key": "token", "user_id": "user-1" } }"#
        let localHash = AIUsageService.identityHash(of: "user-1")
        let sameID = String(localHash.prefix(8))
        let sut = AIUsageService(.testDependencies(
            dataClient: testDependency(of: DataClient.self) {
                $0.read = { url in
                    guard url.path().hasSuffix(".grok/auth.json") else { throw CocoaError(.fileReadNoSuchFile) }
                    return Data(grokAuthJSON.utf8)
                }
            },
            httpClient: testDependency(of: HTTPClient.self) {
                $0.data = { request in
                    let body = request.url?.query()?.contains("xai") == true
                        ? #"{ "accounts": [ { "id": "\#(sameID)", "quota": { "weeklyPercent": 1 } }, { "id": "other123", "quota": { "weeklyPercent": 2 } } ] }"#
                        : #"{ "accounts": [] }"#
                    let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
                    return (Data(body.utf8), response)
                }
            }
        ))
        let bundles = await sut.fetchOpenCodexUsage(excludingLoginsOf: [.grok])
        #expect(bundles.map(\.accountID) == ["openCodex/other123"])
        let unfiltered = await sut.fetchOpenCodexUsage()
        #expect(unfiltered.count == 2)
    }

    @Test(arguments: [
        ("20b539dd", true),
        ("20b539dd-1", true),
        ("20b539dd00000000000000000000000f", false),
        ("abc", false),
        ("ffffffff", false),
    ])
    func isSameIdentity_matches_hash_prefix(accountID: String, expected: Bool) {
        let hash = "20b539dd00000000000000000000000000000000000000000000000000000000"
        #expect(AIUsageService.isSameIdentity(openCodexAccountID: accountID, identityHash: hash) == expected)
    }
}
