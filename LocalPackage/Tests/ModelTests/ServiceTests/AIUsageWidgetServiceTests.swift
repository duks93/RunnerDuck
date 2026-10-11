import AllocatedUnfairLock
import Foundation
import Testing

@testable import DataSource
@testable import Model

struct AIUsageWidgetServiceTests {
    private let homeDirectory = URL(filePath: "/Users/user/")

    private func bundle(percentage: Double, lastUpdatedDate: Date = .distantPast) -> AIUsageBundle {
        AIUsageBundle(
            provider: .claudeCode,
            status: .available([AIUsageWindow(kind: .fiveHour, percentage: percentage)]),
            lastUpdatedDate: lastUpdatedDate
        )
    }

    private func appDependencies(
        files: AllocatedUnfairLock<[URL: Data]>,
        reloadedKinds: AllocatedUnfairLock<[String]>,
        homeDirectory: URL?
    ) -> AppDependencies {
        .testDependencies(
            dataClient: testDependency(of: DataClient.self) {
                $0.read = { url in
                    guard let data = files.withLock({ $0[url] }) else {
                        throw CocoaError(.fileReadNoSuchFile)
                    }
                    return data
                }
                $0.write = { data, url in
                    files.withLock { $0[url] = data }
                }
            },
            fileManagerClient: testDependency(of: FileManagerClient.self) {
                $0.realHomeDirectory = { homeDirectory }
            },
            widgetCenterClient: testDependency(of: WidgetCenterClient.self) {
                $0.reloadTimelines = { kind in
                    reloadedKinds.withLock { $0.append(kind) }
                }
            }
        )
    }

    @Test
    func publish_saves_bundles_and_reloads_widget_when_usage_changes() {
        let files = AllocatedUnfairLock<[URL: Data]>(initialState: [:])
        let reloadedKinds = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = AIUsageWidgetService(appDependencies(files: files, reloadedKinds: reloadedKinds, homeDirectory: homeDirectory))
        sut.publish([bundle(percentage: 10)])
        sut.publish([bundle(percentage: 20)])
        #expect(sut.loadBundles() == [bundle(percentage: 20)])
        #expect(reloadedKinds.withLock { $0 } == [.aiUsageWidgetKind, .aiUsageWidgetKind])
    }

    @Test
    func publish_does_not_reload_widget_when_only_lastUpdatedDate_changes() {
        let files = AllocatedUnfairLock<[URL: Data]>(initialState: [:])
        let reloadedKinds = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = AIUsageWidgetService(appDependencies(files: files, reloadedKinds: reloadedKinds, homeDirectory: homeDirectory))
        let laterDate = Date(timeIntervalSince1970: 1_790_000_000)
        sut.publish([bundle(percentage: 10)])
        sut.publish([bundle(percentage: 10, lastUpdatedDate: laterDate)])
        #expect(sut.loadBundles() == [bundle(percentage: 10, lastUpdatedDate: laterDate)])
        #expect(reloadedKinds.withLock { $0 } == [.aiUsageWidgetKind])
    }

    @Test
    func publish_skips_reload_when_home_directory_is_unavailable() {
        let files = AllocatedUnfairLock<[URL: Data]>(initialState: [:])
        let reloadedKinds = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = AIUsageWidgetService(appDependencies(files: files, reloadedKinds: reloadedKinds, homeDirectory: nil))
        sut.publish([bundle(percentage: 10)])
        #expect(sut.loadBundles().isEmpty)
        #expect(files.withLock { $0.isEmpty })
        #expect(reloadedKinds.withLock { $0.isEmpty })
    }

    @Test
    func loadBundles_returns_empty_when_snapshot_is_missing() {
        let sut = AIUsageWidgetService(.testDependencies())
        #expect(sut.loadBundles().isEmpty)
    }
}
