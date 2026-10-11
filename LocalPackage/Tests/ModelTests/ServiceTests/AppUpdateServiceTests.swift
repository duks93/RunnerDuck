import AllocatedUnfairLock
import Foundation
import Testing

@testable import DataSource
@testable import Model

struct AppUpdateServiceTests {
    private static let commit = "3d9c719e0969785e4b0e8a57f0911b8c63a501dd"

    private static let newerCommit = "25568fdb1e940c160ea8850084df218c1f4c81cb"
    private static let currentFeed = "{\"schemaVersion\":1,\"commit\":\"\(commit)\",\"ancestors\":[]}"
    private static let newerFeed = "{\"schemaVersion\":1,\"commit\":\"\(newerCommit)\",\"ancestors\":[\"\(commit)\"]}"

    @Test
    func check_matching_revision_is_current() async {
        #expect(await service(body: Self.currentFeed).check() == .upToDate)
    }

    @Test
    func check_ancestor_revision_has_update() async {
        #expect(await service(body: Self.newerFeed).check() == .available)
    }

    @Test
    func check_unknown_revision_does_not_suggest_downgrade() async {
        let body = "{\"schemaVersion\":1,\"commit\":\"\(Self.newerCommit)\",\"ancestors\":[]}"
        #expect(await service(body: body).check() == .unpublishedBuild)
    }

    @Test(arguments: [
        currentFeed.replacingOccurrences(of: commit, with: commit.uppercased()),
        newerFeed.replacingOccurrences(of: commit, with: commit.uppercased()),
    ])
    func check_uppercase_feed_revisions_are_rejected(body: String) async {
        #expect(await service(body: body).check() == .failed)
    }

    @Test(arguments: [403, 404, 429, 500])
    func check_http_failure_never_reports_current(statusCode: Int) async {
        #expect(await service(body: Self.currentFeed, statusCode: statusCode).check() == .failed)
    }

    @Test(arguments: [
        "{}", "null", "not json",
        "{\"schemaVersion\":2,\"commit\":\"\(commit)\",\"ancestors\":[]}",
        "{\"schemaVersion\":1,\"commit\":\"bad\",\"ancestors\":[]}",
        "{\"schemaVersion\":1,\"commit\":\"\(commit)\",\"ancestors\":[\"bad\"]}",
        "{\"schemaVersion\":1,\"commit\":\"\(commit)\",\"ancestors\":[\"\(commit)\"]}",
    ])
    func check_invalid_response_reports_failure(body: String) async {
        #expect(await service(body: body).check() == .failed)
    }

    @Test(arguments: ["", "main", "../main", String(repeating: "z", count: 40)])
    func check_invalid_build_does_not_request(commit: String) async {
        let requested = AllocatedUnfairLock(initialState: false)
        let sut = AppUpdateService(.testDependencies(
            buildInfoClient: .init(commit: { commit }),
            httpClient: .init(data: { _ in
                requested.withLock { $0 = true }
                throw URLError(.notConnectedToInternet)
            })
        ))
        #expect(await sut.check() == .unknownBuild)
        #expect(!requested.withLock { $0 })
    }

    @Test
    func check_network_failure_reports_failure() async {
        let sut = AppUpdateService(.testDependencies(buildInfoClient: .init(commit: { Self.commit })))
        #expect(await sut.check() == .failed)
    }

    @Test
    func check_public_feed_sends_no_revision_or_credentials() async {
        let sut = AppUpdateService(.testDependencies(
            buildInfoClient: .init(commit: { Self.commit.uppercased() }),
            httpClient: .init(data: { request in
                #expect(request.url?.absoluteString == "https://github.com/duks93/RunnerDuck/releases/latest/download/latest.json")
                #expect(request.value(forHTTPHeaderField: "User-Agent") == "RunnerDuck")
                #expect(request.value(forHTTPHeaderField: "Authorization") == nil)
                #expect(request.timeoutInterval == 15)
                return (Data(Self.newerFeed.utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
            })
        ))
        #expect(await sut.check() == .available)
        #expect(sut.changesURL?.absoluteString == "https://github.com/duks93/RunnerDuck/releases")
    }

    @MainActor @Test
    func settings_check_and_retry_replace_previous_result() async {
        let responses = AllocatedUnfairLock(initialState: [Self.newerFeed, "invalid"])
        let sut = GeneralSettings(.testDependencies(
            buildInfoClient: .init(commit: { Self.commit }),
            httpClient: .init(data: { request in
                let body = responses.withLock { $0.removeFirst() }
                return (Data(body.utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
            })
        ))
        await sut.send(.checkForUpdates)
        #expect(sut.appUpdateStatus == .available)
        #expect(sut.showsChanges)
        await sut.send(.checkForUpdates)
        #expect(sut.appUpdateStatus == .failed)
        #expect(!sut.showsChanges)
        #expect(sut.canCheckForUpdates)
    }

    private func service(body: String, statusCode: Int = 200) -> AppUpdateService {
        AppUpdateService(.testDependencies(
            buildInfoClient: .init(commit: { Self.commit }),
            httpClient: .init(data: { request in
                (Data(body.utf8), HTTPURLResponse(url: request.url!, statusCode: statusCode, httpVersion: nil, headerFields: nil)!)
            })
        ))
    }
}
