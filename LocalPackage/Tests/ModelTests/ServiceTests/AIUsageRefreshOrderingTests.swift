import AllocatedUnfairLock
import Foundation
import Testing

@testable import DataSource
@testable import Model

struct AIUsageRefreshOrderingTests {
    @Test
    func refresh_old_response_cannot_restore_disabled_source_or_widget_snapshot() async throws {
        let fixture = Fixture()
        defer { fixture.requests.finishAll() }
        let oldRefresh = Task { await AIUsageService(fixture.dependencies).refresh() }
        try #require(await waitUntil { fixture.requests.count == 1 })
        let settingsService = AIUsageService(fixture.dependencies)
        settingsService.setSource(.codex, isEnabled: false)
        await settingsService.refresh()
        try fixture.requests.finish(0, percentage: 90)
        await oldRefresh.value
        #expect(fixture.appState.withLock(\.metrics.latestValue?.aiUsageBundles) == [])
        #expect(AIUsageWidgetService(fixture.dependencies).loadBundles() == [])
    }

    @Test
    func refresh_newest_response_wins_when_requests_complete_out_of_order() async throws {
        let fixture = Fixture()
        defer { fixture.requests.finishAll() }
        let older = Task { await AIUsageService(fixture.dependencies).refresh() }
        try #require(await waitUntil { fixture.requests.count == 1 })
        let newer = Task { await AIUsageService(fixture.dependencies).refresh() }
        try #require(await waitUntil { fixture.requests.count == 2 })
        try fixture.requests.finish(1, percentage: 20)
        await newer.value
        try fixture.requests.finish(0, percentage: 90)
        await older.value
        let expected = [AIUsageBundle(
            provider: .codex,
            status: .available([AIUsageWindow(kind: .fiveHour, percentage: 20)]),
            lastUpdatedDate: .distantPast
        )]
        #expect(fixture.appState.withLock(\.metrics.latestValue?.aiUsageBundles) == expected)
        #expect(AIUsageWidgetService(fixture.dependencies).loadBundles() == expected)
    }

    @Test
    func setSource_invalidates_request_before_configuration_observer_refreshes() async throws {
        let fixture = Fixture()
        defer { fixture.requests.finishAll() }
        let refresh = Task { await AIUsageService(fixture.dependencies).refresh() }
        try #require(await waitUntil { fixture.requests.count == 1 })
        AIUsageService(fixture.dependencies).setSource(.codex, isEnabled: false)
        try fixture.requests.finish(0, percentage: 90)
        await refresh.value
        #expect(fixture.appState.withLock(\.metrics.latestValue) == nil)
        #expect(fixture.files.withLock { $0.isEmpty })
    }

    @Test
    func setSource_allows_synchronous_settings_observers_to_read_app_state() {
        let fixture = Fixture()
        let observed = AllocatedUnfairLock(initialState: false)
        var dependencies = fixture.dependencies
        let save = dependencies.userDefaultsClient.set
        dependencies.userDefaultsClient.set = { [appState = fixture.appState] value, key in
            save(value, key)
            _ = appState.withLock(\.aiUsageRefreshGeneration)
            observed.withLock { $0 = true }
        }
        AIUsageService(dependencies).setSource(.codex, isEnabled: false)
        #expect(observed.withLock { $0 })
        #expect(UserDefaultsRepository(dependencies.userDefaultsClient).aiUsageConfiguration == AIUsageConfiguration(
            overrides: ["codex": false]
        ))
    }

    @Test(arguments: [false, true])
    func refresh_stopped_or_cancelled_request_does_not_publish(cancelTask: Bool) async throws {
        let fixture = Fixture()
        defer { fixture.requests.finishAll() }
        let refresh = Task { await AIUsageService(fixture.dependencies).refresh() }
        try #require(await waitUntil { fixture.requests.count == 1 })
        if cancelTask {
            refresh.cancel()
        } else {
            AIUsageService(fixture.dependencies).stopMonitoring()
        }
        try fixture.requests.finish(0, percentage: 90)
        await refresh.value
        #expect(fixture.appState.withLock(\.metrics.latestValue) == nil)
        #expect(fixture.files.withLock { $0.isEmpty })
    }

    private struct Fixture: Sendable {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        let files = AllocatedUnfairLock<[URL: Data]>(initialState: [:])
        let userDefaultsClient = UserDefaultsClient.storage().client
        let requests = DelayedRequests()

        var dependencies: AppDependencies {
            .testDependencies(
                appStateClient: .testDependency(appState),
                dataClient: testDependency(of: DataClient.self) { [files] in
                    $0.read = { url in
                        if url.path().hasSuffix(".codex/auth.json") {
                            return Data(#"{"tokens":{"access_token":"test-token"}}"#.utf8)
                        }
                        guard let data = files.withLock({ $0[url] }) else {
                            throw CocoaError(.fileReadNoSuchFile)
                        }
                        return data
                    }
                    $0.write = { data, url in files.withLock { $0[url] = data } }
                },
                fileManagerClient: testDependency(of: FileManagerClient.self) {
                    $0.realHomeDirectory = { URL(filePath: "/Users/test/") }
                },
                httpClient: testDependency(of: HTTPClient.self) { [requests] in
                    $0.data = { request in await requests.wait(for: request) }
                },
                userDefaultsClient: userDefaultsClient
            )
        }
    }

    private final class DelayedRequests: Sendable {
        private let state = AllocatedUnfairLock(initialState: State())

        var count: Int { state.withLock(\.started) }

        func wait(for request: URLRequest) async -> (Data, URLResponse) {
            await withCheckedContinuation { continuation in
                state.withLock {
                    $0.pending[$0.started] = continuation
                    $0.started += 1
                }
            }
        }

        func finish(_ index: Int, percentage: Double) throws {
            let continuation = try #require(state.withLock { $0.pending.removeValue(forKey: index) })
            continuation.resume(returning: response(percentage: percentage))
        }

        func finishAll() {
            let pending = state.withLock {
                let pending = Array($0.pending.values)
                $0.pending.removeAll()
                return pending
            }
            pending.forEach { $0.resume(returning: response(percentage: 0)) }
        }

        private func response(percentage: Double) -> (Data, URLResponse) {
            let url = URL(string: "https://example.com/usage")!
            let data = Data("""
                {"rate_limit":{"primary_window":{"used_percent":\(percentage),"limit_window_seconds":18000}}}
                """.utf8)
            return (data, HTTPURLResponse(url: url, statusCode: 200, httpVersion: nil, headerFields: nil)!)
        }

        private struct State {
            var started = 0
            var pending = [Int: CheckedContinuation<(Data, URLResponse), Never>]()
        }
    }
}
