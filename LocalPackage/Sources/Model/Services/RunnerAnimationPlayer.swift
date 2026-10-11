// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

import DataSource
import Foundation

public final class RunnerAnimationPlayer {
    private let animation: RunnerAnimation
    private var elapsed: Double = 0
    private var lastLoadTime: Double?
    private var filteredLoad: Double = 0
    private var transitionTarget: RunnerMotionState?
    public private(set) var state = RunnerMotionState.walk
    public private(set) var target = RunnerMotionState.walk
    public private(set) var clipName = "walk"
    public var isTransitioning: Bool { transitionTarget != nil }
    public var frameIndex: Int {
        let clip = animation.clips[clipName]!
        return clip.frames[min(clip.frames.count - 1, Int(elapsed * clip.fps))]
    }

    public init(animation: RunnerAnimation) { self.animation = animation }

    public func updateLoad(_ percentage: Double?, reversed: Bool, nowSeconds: Double) {
        guard nowSeconds.isFinite else { return }
        var load = percentage.flatMap { $0.isFinite ? min(100, max(0, $0)) : nil } ?? 0
        if percentage?.isFinite == true && reversed { load = 100 - load }
        let weight = lastLoadTime.map { 1 - exp(-max(0, nowSeconds - $0) / 2) } ?? 1
        filteredLoad += weight * (load - filteredLoad)
        lastLoadTime = nowSeconds
        target = if filteredLoad >= 60 || (target == .run && filteredLoad > 55) {
            .run
        } else { .walk }
    }

    public func advance(seconds: Double) {
        guard seconds.isFinite, seconds > 0 else { return }
        var remaining = min(seconds, 10)
        while remaining > 0.0000001 {
            if !isTransitioning && elapsed < 0.0000001 && state != target { beginTransition() }
            let clip = animation.clips[clipName]!
            let rate = isTransitioning ? 1 : playbackRate
            let untilBoundary = (clip.duration - elapsed) / rate
            if remaining < untilBoundary {
                elapsed += remaining * rate
                break
            }
            remaining -= untilBoundary
            elapsed = 0
            if let next = transitionTarget {
                state = next
                transitionTarget = nil
                clipName = state.rawValue
            }
        }
    }

    private func beginTransition() {
        let next: RunnerMotionState = state == .walk ? target : .walk
        clipName = "\(state.rawValue)_to_\(next.rawValue)"
        transitionTarget = next
    }

    private var playbackRate: Double {
        switch state {
        case .idle: 1
        case .walk: 0.8 + 0.4 * min(1, max(0, (filteredLoad - 10) / 50))
        case .run: 0.8 + 0.4 * min(1, max(0, (filteredLoad - 55) / 45))
        }
    }
}
