// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

import DataSource
import Foundation

public final class RunnerAnimationPlayer {
    private let animation: RunnerAnimation
    private var elapsed: Double = 0
    private var lastLoadTime: Double?
    private var filteredLoad: Double = 0
    public var playbackRate: Double { 0.8 + 1.2 * filteredLoad / 100 }
    public var frameIndex: Int {
        let clip = animation.clips["walk"]!
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
    }

    public func advance(seconds: Double) {
        guard seconds.isFinite, seconds > 0 else { return }
        elapsed = (elapsed + min(seconds, 10) * playbackRate)
            .truncatingRemainder(dividingBy: animation.clips["walk"]!.duration)
    }
}
