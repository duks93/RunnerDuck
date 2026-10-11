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
        #expect(await service(body: Self.newerFeed).check() == .available(Self.newerCommit))
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
        #expect(await sut.check() == .available(Self.newerCommit))
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
        #expect(sut.appUpdateStatus == .available(Self.newerCommit))
        #expect(sut.isUpdateAvailable)
        await sut.send(.checkForUpdates)
        #expect(sut.appUpdateStatus == .failed)
        #expect(!sut.isUpdateAvailable)
        #expect(sut.canCheckForUpdates)
    }

    @Test
    func installUpdate_downloads_validated_source_and_runs_installer() async {
        let requestedURL = AllocatedUnfairLock<URL?>(initialState: nil)
        let writtenURLs = AllocatedUnfairLock<[URL]>(initialState: [])
        let invocations = AllocatedUnfairLock<[[String]]>(initialState: [])
        let sut = AppUpdateService(.testDependencies(
            dataClient: .init(read: { _ in Data() }, write: { _, url in writtenURLs.withLock { $0.append(url) } }, convert: { _, _ in Data() }),
            fileManagerClient: Self.fileManagerClient,
            httpClient: .init(data: { request in
                requestedURL.withLock { $0 = request.url }
                return (Data("archive".utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
            }),
            processClient: .init(run: { executableURL, arguments, logURL in
                #expect(logURL.path(percentEncoded: false) == "/Users/user/Library/Caches/RunnerDuck/Update/update.log")
                invocations.withLock { $0.append([executableURL.path(percentEncoded: false)] + arguments) }
                return .zero
            })
        ))
        #expect(await sut.installUpdate(commit: Self.newerCommit))
        #expect(requestedURL.withLock(\.self)?.absoluteString == "https://github.com/duks93/RunnerDuck/archive/\(Self.newerCommit).tar.gz")
        #expect(writtenURLs.withLock(\.self).map { $0.lastPathComponent } == ["update.log", "source.tar.gz"])
        let update = "/Users/user/Library/Caches/RunnerDuck/Update/"
        #expect(invocations.withLock(\.self) == [
            ["/usr/bin/tar", "-xzf", update + "source.tar.gz", "-C", update],
            [
                "/bin/bash",
                "-c",
                """
                    app_pid=$PPID
                    RUNNERDUCK_SOURCE_COMMIT="$2" /bin/bash "$1/install.sh" --no-launch || exit
                    rm -rf "$1"
                    kill "$app_pid"
                    for _ in {1..50}; do kill -0 "$app_pid" 2>/dev/null || break; sleep 0.2; done
                    open "$HOME/Applications/RunnerDuck.app"
                    """,
                "runnerduck-update",
                update + "RunnerDuck-\(Self.newerCommit)/",
                Self.newerCommit,
            ],
        ])
    }

    @Test
    func installUpdate_reports_failure_when_installer_fails() async {
        let sut = AppUpdateService(.testDependencies(
            fileManagerClient: Self.fileManagerClient,
            httpClient: .init(data: { request in
                (Data("archive".utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
            }),
            processClient: .init(run: { executableURL, _, _ in
                executableURL.lastPathComponent == "tar" ? .zero : 1
            })
        ))
        #expect(!(await sut.installUpdate(commit: Self.newerCommit)))
    }

    @Test
    func installUpdate_does_not_run_installer_when_download_fails() async {
        let ran = AllocatedUnfairLock(initialState: false)
        let sut = AppUpdateService(.testDependencies(
            fileManagerClient: Self.fileManagerClient,
            httpClient: .init(data: { request in
                (Data(), HTTPURLResponse(url: request.url!, statusCode: 404, httpVersion: nil, headerFields: nil)!)
            }),
            processClient: .init(run: { _, _, _ in
                ran.withLock { $0 = true }
                return .zero
            })
        ))
        #expect(!(await sut.installUpdate(commit: Self.newerCommit)))
        #expect(!ran.withLock { $0 })
    }

    @Test(arguments: ["", "main", commit.uppercased(), "../\(String(repeating: "a", count: 37))"])
    func installUpdate_rejects_invalid_commit_without_request(commit: String) async {
        let requested = AllocatedUnfairLock(initialState: false)
        let sut = AppUpdateService(.testDependencies(
            fileManagerClient: Self.fileManagerClient,
            httpClient: .init(data: { _ in
                requested.withLock { $0 = true }
                throw URLError(.notConnectedToInternet)
            })
        ))
        #expect(!(await sut.installUpdate(commit: commit)))
        #expect(!requested.withLock { $0 })
    }

    @MainActor @Test
    func settings_failed_installation_shows_log() async {
        let selectedURLs = AllocatedUnfairLock<[URL]>(initialState: [])
        var nsWorkspaceClient = NSWorkspaceClient.testValue
        nsWorkspaceClient.activateFileViewerSelecting = { urls in selectedURLs.withLock { $0 = urls } }
        let sut = GeneralSettings(.testDependencies(
            buildInfoClient: .init(commit: { Self.commit }),
            fileManagerClient: Self.fileManagerClient,
            httpClient: .init(data: { request in
                let body = request.url?.lastPathComponent == "latest.json" ? Self.newerFeed : "archive"
                return (Data(body.utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
            }),
            nsWorkspaceClient: nsWorkspaceClient,
            processClient: .init(run: { _, _, _ in 1 })
        ))
        await sut.send(.checkForUpdates)
        await sut.send(.installUpdate)
        #expect(sut.appUpdateStatus == .installFailed)
        #expect(sut.showsUpdateLog)
        #expect(sut.canCheckForUpdates)
        await sut.send(.showUpdateLog)
        #expect(selectedURLs.withLock(\.self).map { $0.path(percentEncoded: false) } == ["/Users/user/Library/Caches/RunnerDuck/Update/update.log"])
    }

    private static let fileManagerClient: FileManagerClient = {
        var fileManagerClient = FileManagerClient.testValue
        fileManagerClient.urls = { _, _ in [URL(filePath: "/Users/user/Library/Caches/", directoryHint: .isDirectory)] }
        return fileManagerClient
    }()

    private func service(body: String, statusCode: Int = 200) -> AppUpdateService {
        AppUpdateService(.testDependencies(
            buildInfoClient: .init(commit: { Self.commit }),
            httpClient: .init(data: { request in
                (Data(body.utf8), HTTPURLResponse(url: request.url!, statusCode: statusCode, httpVersion: nil, headerFields: nil)!)
            })
        ))
    }
}
