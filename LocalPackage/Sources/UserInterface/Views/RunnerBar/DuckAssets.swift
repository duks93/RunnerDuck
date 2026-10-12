// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

import AppKit
import DataSource

@MainActor
enum DuckAssets {
    private static var cache: [RunnerKind: (animation: RunnerAnimation, frames: [CGImage])?] = [:]

    static func bundle(for kind: RunnerKind) -> (animation: RunnerAnimation, frames: [CGImage])? {
        if let cached = cache[kind] { return cached }
        // The duck variants share one manifest; each white runner ships its own manifest and atlas.
        let loaded = switch kind {
        case .duck: load(directory: "Duck", manifest: "animation", atlas: "duck-atlas.png", image: "duck-atlas")
        case .duckFilled: load(directory: "Duck", manifest: "animation", atlas: "duck-atlas.png", image: "duck-filled-atlas")
        default: load(directory: "Runners", manifest: kind.id, atlas: "\(kind.id).png", image: kind.id)
        }
        cache[kind] = loaded
        return loaded
    }

    private static func load(
        directory: String,
        manifest: String,
        atlas: String,
        image: String
    ) -> (animation: RunnerAnimation, frames: [CGImage])? {
        do {
            guard let url = Bundle.module.url(forResource: manifest, withExtension: "json", subdirectory: directory),
                  let atlasURL = Bundle.module.url(forResource: image, withExtension: "png", subdirectory: directory),
                  let source = NSImage(contentsOf: atlasURL),
                  let atlasImage = source.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
                throw CocoaError(.fileNoSuchFile)
            }
            let animation = try RunnerAnimation.parse(Data(contentsOf: url), atlas: atlas)
            let frames = try animation.frames.map { frame in
                guard frame.x <= atlasImage.width - frame.width, frame.y <= atlasImage.height - frame.height,
                      let cell = atlasImage.cropping(to: CGRect(x: frame.x, y: frame.y, width: frame.width, height: frame.height)) else {
                    throw CocoaError(.fileReadCorruptFile)
                }
                return cell
            }
            return (animation, frames)
        } catch {
            NSLog("RunnerDuck %@ could not load: %@", image, error.localizedDescription)
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
