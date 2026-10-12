import AppKit
import DataSource
import Foundation
import Testing
@testable import UserInterface

struct DuckRenderingTests {
    @MainActor @Test(arguments: RunnerKind.allCases, RunnerSize.allCases)
    func selected_size_keeps_layers_aligned_and_preserves_the_current_frame(kind: RunnerKind, size: RunnerSize) throws {
        let height = CGFloat(size.menuBarHeight)
        let layer = RunnerLayer(gap: 4, containerHeight: 22)
        let background = BackgroundLayer(gap: 4, containerHeight: 22)
        #expect(layer.setDuck(kind: kind, flipped: false))
        let initial = try #require(kind.isTemplate ? layer.mask?.contents : layer.contents)
        #expect(layer.setDuck(kind: kind, flipped: false, height: height))
        let resized = try #require(kind.isTemplate ? layer.mask?.contents : layer.contents)
        background.setSize(CGSize(width: height * 1.5, height: height))
        #expect(ObjectIdentifier(initial as AnyObject) == ObjectIdentifier(resized as AnyObject))
        #expect(layer.frame == background.frame)
        #expect(layer.frame == CGRect(x: 4, y: (22 - height) / 2, width: height * 1.5, height: height))
        #expect(layer.frame.minY >= 0 && layer.frame.maxY <= 22)
        #expect(layer.setDuck(kind: kind, flipped: true, height: height))
        #expect(layer.frame == background.frame)
    }

    @MainActor @Test(arguments: RunnerKind.allCases, [nil, 0.0] as [Double?])
    func default_walk_updates_layer_contents_without_cpu_load(kind: RunnerKind, percentage: Double?) async throws {
        let layer = RunnerLayer(gap: 0)
        layer.setLoad(.init(percentage: percentage, reversed: false))
        #expect(layer.setDuck(kind: kind, flipped: false))
        let initialContents = try #require(kind.isTemplate ? layer.mask?.contents : layer.contents)
        var frames = Set([ObjectIdentifier(initialContents as AnyObject)])
        for _ in 0..<8 {
            try await Task.sleep(for: .milliseconds(50))
            let contents = try #require(kind.isTemplate ? layer.mask?.contents : layer.contents)
            frames.insert(ObjectIdentifier(contents as AnyObject))
        }
        #expect(frames.count >= 3)
    }

    @MainActor @Test func menu_bar_layers_support_core_animation_presentation_copies() throws {
        let window = NSWindow(
            contentRect: CGRect(x: 0, y: 0, width: 100, height: 30),
            styleMask: .borderless,
            backing: .buffered,
            defer: false
        )
        window.isReleasedWhenClosed = false
        defer { window.close() }
        let view = try #require(window.contentView)
        view.wantsLayer = true
        let rootLayer = try #require(view.layer)
        let backgroundLayer = BackgroundLayer(gap: 4)
        let runnerLayer = RunnerLayer(gap: 4)
        rootLayer.addSublayer(backgroundLayer)
        rootLayer.addSublayer(runnerLayer)
        window.orderFront(nil)
        backgroundLayer.setSize(CGSize(width: 27, height: 18))
        #expect(runnerLayer.setDuck(flipped: false))
        runnerLayer.setColor(CGColor(gray: 0, alpha: 1), true)
        CATransaction.flush()
        let runnerPresentation = try #require(runnerLayer.presentation())
        let backgroundPresentation = try #require(backgroundLayer.presentation())
        #expect(runnerPresentation !== runnerLayer)
        #expect(backgroundPresentation !== backgroundLayer)
        #expect(runnerPresentation.mask?.contents != nil)
        #expect(backgroundPresentation.frame == backgroundLayer.frame)
        #expect(runnerLayer.setDuck(flipped: true))
        CATransaction.flush()
        #expect(runnerLayer.presentation()?.mask?.contents != nil)
    }

    @MainActor @Test(arguments: RunnerKind.allCases)
    func bundledClipsRenderAtMenuBarSizesAndFlip(kind: RunnerKind) throws {
        let bundle = try #require(DuckAssets.bundle(for: kind))
        #expect(bundle.animation.clips.count == 1)
        #expect(bundle.frames.count == 8)
        #expect(bundle.animation.clips["walk"]?.frames == Array(0..<8))
        for index in 0..<8 {
            #expect(DuckAssets.image(named: "\(kind.id)-frame-\(index)") != nil)
        }
        #expect(DuckAssets.image(named: "\(kind.id)-frame-8") == nil)
        // xcodebuild may filter environment variables before launching a test bundle.
        let root = URL(filePath: ProcessInfo.processInfo.environment["RUNNERDUCK_RENDER_DIR"]
            ?? "/tmp/RunnerDuckRenderValidation")
        let output = kind == .duck ? root : root.appending(path: kind.id)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        for (name, clip) in bundle.animation.clips {
            let width = clip.frames.count * 48
            let context = try #require(CGContext(data: nil, width: width, height: 160,
                bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue))
            for row in 0..<4 {
                let dark = row >= 2
                let height = row % 2 == 0 ? 18.0 : 24.0
                context.setFillColor(dark ? CGColor(gray: 0.12, alpha: 1) : CGColor(gray: 1, alpha: 1))
                context.fill(CGRect(x: 0, y: row * 40, width: width, height: 40))
                for (index, frameIndex) in clip.frames.enumerated() {
                    let image = bundle.frames[frameIndex]
                    #expect(image.width == 192 && image.height == 128)
                    context.saveGState()
                    context.translateBy(x: Double(index * 48) + 4, y: Double(row * 40) + 8)
                    if dark { context.translateBy(x: height * 1.5, y: 0); context.scaleBy(x: -1, y: 1) }
                    let rect = CGRect(x: 0, y: 0, width: height * 1.5, height: height)
                    if kind.isTemplate {
                        context.clip(to: rect, mask: image)
                        context.setFillColor(dark ? CGColor(gray: 1, alpha: 1) : CGColor(gray: 0, alpha: 1))
                        context.fill(rect)
                    } else {
                        context.draw(image, in: rect)
                    }
                    context.restoreGState()
                }
            }
            let rendered = try #require(context.makeImage())
            let png = try #require(NSBitmapImageRep(cgImage: rendered).representation(using: .png, properties: [:]))
            try png.write(to: output.appending(path: name + ".png"))
        }
        // Reconfiguration uses the same layer and cached player without disrupting its mask.
        let layer = RunnerLayer(gap: 0)
        #expect(layer.setDuck(kind: kind, flipped: false))
        layer.setLoad(.init(percentage: 100, reversed: false))
        layer.setColor(CGColor(gray: 1, alpha: 1), kind.isTemplate)
        #expect(layer.setDuck(kind: kind, flipped: true))
        #expect((kind.isTemplate ? layer.mask?.contents : layer.contents) != nil)
        #expect(layer.frame.height == 18)
    }

    @MainActor @Test func filled_duck_preserves_white_body_and_switches_rendering_mode() throws {
        let image = try #require(DuckAssets.image(named: "duck-filled-frame-0"))
        let cgImage = try #require(image.cgImage(forProposedRect: nil, context: nil, hints: nil))
        let bitmap = NSBitmapImageRep(cgImage: cgImage)
        let body = try #require(bitmap.colorAt(x: 70, y: 70)?.usingColorSpace(.deviceRGB))
        #expect(body.alphaComponent > 0.95 && body.redComponent > 0.95)
        #expect(!Runner(kind: .duckFilled).isTemplate)
        #expect(DuckAssets.image(named: "missing-frame-0") == nil)
        let layer = RunnerLayer(gap: 0)
        for kind in [RunnerKind.duck, .duckFilled, .duck, .duckFilled] {
            layer.setColor(CGColor(gray: 0, alpha: 1), kind.isTemplate)
            #expect(layer.setDuck(kind: kind, flipped: false))
            #expect((layer.mask != nil) == kind.isTemplate)
            #expect((layer.contents == nil) == kind.isTemplate)
            if !kind.isTemplate { #expect(layer.backgroundColor == nil) }
        }
        let context = try #require(CGContext(data: nil, width: 54, height: 36,
            bitsPerComponent: 8, bytesPerRow: 54 * 4, space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue))
        context.scaleBy(x: 2, y: 2)
        layer.render(in: context)
        let rendered = NSBitmapImageRep(cgImage: try #require(context.makeImage()))
        let renderedBody = try #require(rendered.colorAt(x: 20, y: 20)?.usingColorSpace(.deviceRGB))
        #expect(renderedBody.alphaComponent > 0.9 && renderedBody.redComponent > 0.9)
    }
}
