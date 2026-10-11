import AllocatedUnfairLock
import AppKit
import Foundation
import Testing

@testable import DataSource
@testable import Model

struct RunnerBarTests {
    private func makeEventBridge(recording callStack: AllocatedUnfairLock<[String]>) -> RunnerBar.Action.EventBridge {
        RunnerBar.Action.EventBridge(
            getBundleImage: { name in
                callStack.withLock { $0.append("getBundleImage: \(name)") }
                return NSImage(size: CGSize(width: 10, height: 18))
            },
            setSize: { size in
                callStack.withLock { $0.append("setSize: \(size.width)x\(size.height)") }
            },
            setFrames: { images, isTemplate in
                callStack.withLock { $0.append("setFrames: \(images.count), \(isTemplate)") }
            },
            setColor: { _, _ in },
            setSpeed: { speed in
                callStack.withLock { $0.append("setSpeed: \(speed)") }
            }
        )
    }

    @MainActor @Test
    func send_viewAppeared_applies_latest_runner_bundle_to_event_bridge() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock {
            $0.runnerBundles.send(RunnerBundle(
                runner: .default,
                frames: [.preset("duck-frame-0"), .preset("duck-frame-1")]
            ))
        }
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        await waitUntil { callStack.withLock(\.self).count >= 4 }
        #expect(sut.isReady)
        #expect(sut.size == CGSize(width: 10, height: 18))
        #expect(sut.isTemplate)
        #expect(callStack.withLock(\.self) == [
            "getBundleImage: duck-frame-0",
            "getBundleImage: duck-frame-1",
            "setSize: 10.0x18.0",
            "setFrames: 2, true",
        ])
        await sut.send(.viewDisappeared)
    }

    @MainActor @Test
    func send_viewAppeared_applies_runner_speed_to_event_bridge() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock { $0.runnerSpeeds.send(12.5) }
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        await waitUntil { !callStack.withLock(\.self).isEmpty }
        #expect(callStack.withLock(\.self) == ["setSpeed: 12.5"])
        await sut.send(.viewDisappeared)
    }

    @MainActor @Test(arguments: RunnerSize.allCases)
    func send_viewAppeared_sizes_the_fallback_icon_and_duck_layer_together(size: RunnerSize) async throws {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock {
            $0.runnerBundles.send(RunnerBundle(runner: .default, frames: [.preset("duck-frame-0")]))
        }
        let client = testDependency(of: UserDefaultsClient.self) {
            $0.integer = { key in key == "RUNNER_SIZE" ? size.rawValue : 0 }
        }
        let height = AllocatedUnfairLock<CGFloat?>(initialState: nil)
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        var bridge = makeEventBridge(recording: callStack)
        bridge.setDuck = { _, _, value in
            height.withLock { $0 = value }
            return true
        }
        let sut = RunnerBar(.testDependencies(
            appStateClient: .testDependency(appState),
            userDefaultsClient: client
        ))
        await sut.send(.viewAppeared("RunnerBarTests", bridge))
        await waitUntil { height.withLock { $0 != nil } }
        let appliedHeight = try #require(height.withLock { $0 })
        #expect(appliedHeight == CGFloat(size.menuBarHeight))
        #expect(sut.size == CGSize(width: 10 * appliedHeight / 18, height: appliedHeight))
        #expect(sut.icon?.size == sut.size)
        #expect(!callStack.withLock { $0.contains(where: { $0.hasPrefix("setFrames:") }) })
        await sut.send(.viewDisappeared)
    }

    @MainActor @Test
    func send_viewAppeared_ignores_thumbnail_bundle() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock {
            $0.runnerBundles.send(RunnerBundle(runner: .default, frame: .preset("duck-frame-0")))
        }
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        try? await Task.sleep(for: .milliseconds(50))
        #expect(callStack.withLock(\.self).isEmpty)
        #expect(sut.icon == nil)
        await sut.send(.viewDisappeared)
    }

    @MainActor @Test
    func selecting_size_updates_the_running_menu_bar_without_reopening_it() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock {
            $0.runnerBundles.send(RunnerBundle(runner: .default, frames: [.preset("duck-frame-0")]))
        }
        let savedSize = AllocatedUnfairLock<Int>(initialState: RunnerSize.standard.rawValue)
        let client = testDependency(of: UserDefaultsClient.self) {
            $0.integer = { key in key == "RUNNER_SIZE" ? savedSize.withLock(\.self) : 0 }
            $0.set = { value, key in
                guard key == "RUNNER_SIZE", let value = value as? Int else { return }
                savedSize.withLock { $0 = value }
            }
        }
        let dependencies = AppDependencies.testDependencies(
            appStateClient: .testDependency(appState),
            userDefaultsClient: client
        )
        let height = AllocatedUnfairLock<CGFloat>(initialState: 0)
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        var bridge = makeEventBridge(recording: callStack)
        bridge.setDuck = { _, _, value in
            height.withLock { $0 = value }
            return true
        }
        let bar = RunnerBar(dependencies)
        let settings = RunnerSettings(dependencies)
        await bar.send(.viewAppeared("RunnerBarTests", bridge))
        await waitUntil { height.withLock(\.self) == 18 }
        await settings.send(.runnerSizeSelected(.large))
        await waitUntil { height.withLock(\.self) == 22 }
        #expect(bar.size.height == 22)
        #expect(settings.runnerSize == .large)
        await bar.send(.viewDisappeared)
    }

    @MainActor @Test
    func send_viewAppeared_with_broken_frames_clears_icon() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        appState.withLock {
            $0.runnerBundles.send(RunnerBundle(runner: .default, frames: [.broken]))
        }
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        await waitUntil { callStack.withLock(\.self).count >= 2 }
        #expect(sut.icon == nil)
        #expect(sut.size == .zero)
        #expect(callStack.withLock(\.self) == [
            "setSize: 0.0x0.0",
            "setFrames: 0, true",
        ])
        await sut.send(.viewDisappeared)
    }

    @MainActor @Test
    func send_viewDisappeared_stops_observing_streams() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        await sut.send(.viewDisappeared)
        appState.withLock { $0.runnerSpeeds.send(5.0) }
        try? await Task.sleep(for: .milliseconds(50))
        #expect(callStack.withLock(\.self).isEmpty)
    }
    @MainActor @Test
    func corrupt_custom_image_recovers_to_default_duck() async {
        let appState = AllocatedUnfairLock<AppState>(initialState: .init())
        let custom = Runner(id: "broken-custom", name: "Broken", isTemplate: false, frameOrder: .custom([0]))
        appState.withLock { $0.runnerBundles.send(RunnerBundle(runner: custom, frames: [.custom(Data([0, 1, 2]))])) }
        let callStack = AllocatedUnfairLock<[String]>(initialState: [])
        let sut = RunnerBar(.testDependencies(appStateClient: .testDependency(appState)))
        await sut.send(.viewAppeared("RunnerBarTests", makeEventBridge(recording: callStack)))
        await waitUntil { callStack.withLock { $0.contains("setFrames: 8, true") } }
        #expect(appState.withLock(\.runnerBundles.latestValue)?.runner == .default)
        #expect(sut.icon != nil)
        await sut.send(.viewDisappeared)
    }

}
