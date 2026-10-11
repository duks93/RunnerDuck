import DataSource
import Foundation
import Testing
@testable import Model

struct RunnerAnimationPlayerTests {
    private func animation() -> RunnerAnimation {
        .init(frames: (0..<7).map { .init(x: $0 * 32, y: 0, width: 32, height: 24) },
              clips: Dictionary(uniqueKeysWithValues: RunnerAnimation.requiredClips.map { name in
                  (name, name.contains("_to_")
                   ? RunnerAnimationClip(frames: [4, 5, 6], fps: 10, loop: false)
                   : RunnerAnimationClip(frames: [0, 1, 2, 3], fps: 4, loop: true))
              }))
    }

    @Test func default_walk_advances_before_first_cpu_sample_and_at_zero_load() {
        let player = RunnerAnimationPlayer(animation: animation())
        #expect(player.state == .walk)
        #expect(player.target == .walk)
        #expect(player.clipName == "walk")
        player.advance(seconds: 0.4)
        #expect(player.frameIndex == 1)
        player.updateLoad(0, reversed: false, nowSeconds: 0)
        player.advance(seconds: 0.4)
        #expect(player.frameIndex == 2)
        #expect(player.state == .walk)
    }

    @Test func thresholdsAndHysteresis() {
        let player = RunnerAnimationPlayer(animation: animation())
        let loads = [0.0, 14.99, 15, 14, 10.01, 10, 59.99, 60, 59, 55.01, 55]
        let states: [RunnerMotionState] = [.walk, .walk, .walk, .walk, .walk, .walk, .walk, .run, .run, .run, .walk]
        for index in loads.indices {
            player.updateLoad(loads[index], reversed: false, nowSeconds: Double(index * 1000))
            #expect(player.target == states[index])
        }
    }

    @Test func smoothedSpikeAndReversedLoad() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.updateLoad(0, reversed: false, nowSeconds: 0)
        player.updateLoad(100, reversed: false, nowSeconds: 0.1)
        #expect(player.target == .walk)
        player.updateLoad(0, reversed: true, nowSeconds: 1000)
        #expect(player.target == .run)
        player.updateLoad(100, reversed: true, nowSeconds: 2000)
        #expect(player.target == .walk)
    }

    @Test func missing_and_invalid_samples_keep_walking_when_reversed() {
        for load: Double? in [nil, .nan, .infinity] {
            let player = RunnerAnimationPlayer(animation: animation())
            player.updateLoad(load, reversed: true, nowSeconds: 0)
            #expect(player.target == .walk)
        }
    }

    @Test func high_load_transitions_from_default_walk_to_run_in_300ms() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.updateLoad(100, reversed: false, nowSeconds: 0)
        player.advance(seconds: 0.15)
        #expect(player.clipName == "walk_to_run")
        #expect(player.frameIndex == 5)
        player.advance(seconds: 0.16)
        #expect(player.state == .run)
        #expect(player.clipName == "run")
        #expect(!player.isTransitioning)
    }

    @Test func queuedTargetIsAppliedAfterTransition() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.updateLoad(100, reversed: false, nowSeconds: 0)
        player.advance(seconds: 0.1)
        player.updateLoad(0, reversed: false, nowSeconds: 1000)
        #expect(player.clipName == "walk_to_run")
        player.advance(seconds: 0.21)
        #expect(player.clipName == "run_to_walk")
        player.advance(seconds: 0.3)
        #expect(player.state == .walk)
    }

    @Test func waitsForConnectionPoseAndHandlesSuspend() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.advance(seconds: 0.2)
        player.updateLoad(100, reversed: false, nowSeconds: 0)
        player.advance(seconds: 0.69)
        #expect(player.clipName == "walk")
        player.advance(seconds: 0.02)
        #expect(player.clipName == "walk_to_run")
        for elapsed in [Double.nan, .infinity, -1, 0, 1000000, 0.033] {
            player.advance(seconds: elapsed)
            #expect((0...6).contains(player.frameIndex))
        }
    }

    @Test func manifestValidatesReferencesAndRequiredClips() throws {
        let value = animation()
        #expect(try RunnerAnimation.parse(JSONEncoder().encode(value)) == value)
        var bad = value
        bad.clips.removeValue(forKey: "run_to_walk")
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.clips["walk"] = .init(frames: [999], fps: 12, loop: true)
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.atlas = "../external.png"
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
    }
}
