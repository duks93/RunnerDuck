// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

import Foundation

public struct RunnerAnimationFrame: Codable, Sendable, Equatable {
    public var x: Int
    public var y: Int
    public var width: Int
    public var height: Int
    public init(x: Int, y: Int, width: Int, height: Int) {
        self.x = x; self.y = y; self.width = width; self.height = height
    }
}

public struct RunnerAnimationClip: Codable, Sendable, Equatable {
    public var frames: [Int]
    public var fps: Double
    public var loop: Bool
    public var duration: Double { Double(frames.count) / fps }
    public init(frames: [Int], fps: Double, loop: Bool) {
        self.frames = frames; self.fps = fps; self.loop = loop
    }
}

public struct RunnerAnimation: Codable, Sendable, Equatable {
    public var version: Int
    public var atlas: String
    public var frames: [RunnerAnimationFrame]
    public var clips: [String: RunnerAnimationClip]

    public init(version: Int = 1, atlas: String = "duck-atlas.png", frames: [RunnerAnimationFrame], clips: [String: RunnerAnimationClip]) {
        self.version = version; self.atlas = atlas; self.frames = frames; self.clips = clips
    }

    public static func parse(_ data: Data, atlas: String = "duck-atlas.png") throws -> Self {
        let animation = try JSONDecoder().decode(Self.self, from: data)
        guard animation.version == 1, animation.atlas == atlas,
              (1 ... 256).contains(animation.frames.count),
              animation.frames.allSatisfy({ $0.x >= 0 && $0.y >= 0 && $0.width > 0 && $0.height > 0 }),
              animation.clips.count == 1, let clip = animation.clips["walk"],
              clip.fps.isFinite, clip.fps > 0, clip.fps <= 60, !clip.frames.isEmpty, clip.loop,
              clip.frames.allSatisfy({ animation.frames.indices.contains($0) }) else {
            throw CocoaError(.fileReadCorruptFile)
        }
        return animation
    }
}

public struct RunnerLoad: Sendable {
    public var percentage: Double?
    public var reversed: Bool
    public init(percentage: Double?, reversed: Bool) {
        self.percentage = percentage; self.reversed = reversed
    }
}
