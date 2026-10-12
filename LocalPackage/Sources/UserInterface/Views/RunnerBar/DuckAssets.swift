// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

import AppKit
import DataSource

@MainActor
enum DuckAssets {
    private static let outline = load(atlasName: "duck-atlas")
    private static let filled = load(atlasName: "duck-filled-atlas")

    static func bundle(for kind: RunnerKind) -> (animation: RunnerAnimation, frames: [CGImage])? {
        switch kind {
        case .duck: outline
        case .duckFilled: filled
        }
    }

    private static func load(atlasName: String) -> (animation: RunnerAnimation, frames: [CGImage])? {
        do {
            guard let url = Bundle.module.url(forResource: "animation", withExtension: "json", subdirectory: "Duck"),
                  let atlasURL = Bundle.module.url(forResource: atlasName, withExtension: "png", subdirectory: "Duck"),
                  let source = NSImage(contentsOf: atlasURL),
                  let atlas = source.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
                throw CocoaError(.fileNoSuchFile)
            }
            let animation = try RunnerAnimation.parse(Data(contentsOf: url))
            let frames = try animation.frames.map { frame in
                guard frame.x <= atlas.width - frame.width, frame.y <= atlas.height - frame.height,
                      let image = atlas.cropping(to: CGRect(x: frame.x, y: frame.y, width: frame.width, height: frame.height)) else {
                    throw CocoaError(.fileReadCorruptFile)
                }
                return image
            }
            return (animation, frames)
        } catch {
            NSLog("RunnerDuck %@ could not load: %@", atlasName, error.localizedDescription)
            return nil
        }
    }

    static func image(named name: String) -> NSImage? {
        if let kind = RunnerKind.allCases.first(where: { name.hasPrefix("\($0.id)-frame-") }),
           let index = Int(name.dropFirst("\(kind.id)-frame-".count)),
           let bundle = bundle(for: kind), let clip = bundle.animation.clips["walk"], clip.frames.indices.contains(index) {
            let image = bundle.frames[clip.frames[index]]
            return NSImage(cgImage: image, size: CGSize(width: image.width, height: image.height))
        }
        return nil
    }
}
