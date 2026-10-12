import DataSource
import Foundation
import Testing
@testable import Model

struct RunnerAnimationPlayerTests {
    private func animation() -> RunnerAnimation {
        .init(frames: (0..<8).map { .init(x: $0 * 32, y: 0, width: 32, height: 24) },
              clips: ["walk": .init(frames: Array(0..<8), fps: 8, loop: true)])
    }

    @Test func default_walk_advances_before_first_cpu_sample_and_at_zero_load() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.advance(seconds: 0.4)
        #expect(player.frameIndex == 2)
        player.updateLoad(0, reversed: false, nowSeconds: 0)
        player.advance(seconds: 0.4)
        #expect(player.frameIndex == 5)
    }

    @Test func load_changes_the_speed_of_the_same_walk() {
        for (load, rate) in [(-10.0, 0.8), (0.0, 0.8), (50.0, 1.4), (100.0, 2.0), (110.0, 2.0)] {
            let player = RunnerAnimationPlayer(animation: animation())
            player.updateLoad(load, reversed: false, nowSeconds: 0)
            #expect(abs(player.playbackRate - rate) < 1e-10)
            player.advance(seconds: 0.4)
            #expect(player.frameIndex == Int(0.4 * rate * 8))
        }
    }

    @Test func brief_spike_is_smoothed_and_reversed_option_changes_speed() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.updateLoad(0, reversed: false, nowSeconds: 0)
        player.updateLoad(100, reversed: false, nowSeconds: 0.1)
        #expect((0.8...0.9).contains(player.playbackRate))
        player.updateLoad(0, reversed: true, nowSeconds: 1000)
        #expect(player.playbackRate == 2)
        player.updateLoad(100, reversed: true, nowSeconds: 2000)
        #expect(player.playbackRate == 0.8)
    }

    @Test func missing_or_invalid_cpu_keeps_walking_even_when_reversed() {
        for load: Double? in [nil, .nan, .infinity] {
            let player = RunnerAnimationPlayer(animation: animation())
            player.updateLoad(load, reversed: true, nowSeconds: 0)
            #expect(player.playbackRate == 0.8)
            player.advance(seconds: 0.4)
            #expect(player.frameIndex == 2)
        }
    }

    @Test func speed_changes_preserve_the_current_pose_and_continue_the_loop() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.advance(seconds: 0.5)
        #expect(player.frameIndex == 3)
        player.updateLoad(100, reversed: false, nowSeconds: 0)
        #expect(player.frameIndex == 3)
        player.advance(seconds: 0.125)
        #expect(player.frameIndex == 5)
        player.advance(seconds: 0.25)
        #expect(player.frameIndex == 1)
    }

    @Test func walk_wraps_after_the_last_frame() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.updateLoad(100, reversed: false, nowSeconds: 0)
        var frames: [Int] = []
        for _ in 0..<9 {
            frames.append(player.frameIndex)
            player.advance(seconds: 1.0 / 16)
        }
        #expect(frames == [0, 1, 2, 3, 4, 5, 6, 7, 0])
    }

    @Test func invalid_time_is_ignored_and_long_suspend_stays_in_bounds() {
        let player = RunnerAnimationPlayer(animation: animation())
        player.advance(seconds: 0.4)
        for elapsed in [Double.nan, .infinity, -1, 0] {
            player.advance(seconds: elapsed)
            #expect(player.frameIndex == 2)
        }
        player.updateLoad(100, reversed: false, nowSeconds: .nan)
        #expect(player.playbackRate == 0.8)
        player.advance(seconds: 1000000)
        #expect((0...7).contains(player.frameIndex))
        player.advance(seconds: 0.2)
        #expect(player.frameIndex == 3)
    }

    @Test func manifest_roundtrips_and_rejects_missing_or_extra_clips_and_bad_references() throws {
        let value = animation()
        #expect(try RunnerAnimation.parse(JSONEncoder().encode(value)) == value)
        var bad = value
        bad.clips.removeValue(forKey: "walk")
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.clips["idle"] = .init(frames: [0], fps: 1, loop: true)
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.clips["walk"] = .init(frames: [999], fps: 8, loop: true)
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.atlas = "../external.png"
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        bad = value
        bad.frames[0].x = -1
        #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
    }

    @Test func manifest_rejects_invalid_timing_and_non_looping_or_empty_walk() {
        for clip in [
            RunnerAnimationClip(frames: [0, 1], fps: 0, loop: true),
            RunnerAnimationClip(frames: [0, 1], fps: -1, loop: true),
            RunnerAnimationClip(frames: [0, 1], fps: 61, loop: true),
            RunnerAnimationClip(frames: [0, 1], fps: 8, loop: false),
            RunnerAnimationClip(frames: [], fps: 8, loop: true),
        ] {
            var bad = animation()
            bad.clips["walk"] = clip
            #expect(throws: (any Error).self) { try RunnerAnimation.parse(JSONEncoder().encode(bad)) }
        }
    }
}
