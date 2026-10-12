/*
 RunnerLayer.swift
 UserInterface

 Created by Takuto Nakamura on 2026/05/08.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

import AppKit
import DataSource
import Model

@MainActor
final class RunnerLayer: CALayer {
    private var duckPlayer: RunnerAnimationPlayer?
    private var duckImages: [CGImage] = []
    private var duckTimer: Timer?
    private var lastTick: Double = 0
    private var lastFrame = -1
    private var load = RunnerLoad(percentage: nil, reversed: false)
    private let gap: CGFloat
    private let containerHeight: CGFloat
    private var width = CGFloat.zero
    private let maskLayer = CALayer()
    private var keyFrameAnimation = CAKeyframeAnimation(keyPath: "contents")

    init(gap: CGFloat, containerHeight: CGFloat = 22) {
        self.gap = gap
        self.containerHeight = containerHeight
        super.init()
        contentsGravity = CALayerContentsGravity.left
        masksToBounds = true
        contentsScale = 2.0
        maskLayer.frame = bounds
        keyFrameAnimation.calculationMode = .discrete
        keyFrameAnimation.repeatCount = .infinity
        keyFrameAnimation.isRemovedOnCompletion = false
    }

    override init(layer: Any) {
        guard let runnerLayer = layer as? RunnerLayer else {
            preconditionFailure("Expected a RunnerLayer presentation source.")
        }
        gap = runnerLayer.gap
        containerHeight = runnerLayer.containerHeight
        super.init(layer: layer)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    isolated deinit { duckTimer?.invalidate() }

    func setLoad(_ load: RunnerLoad) {
        self.load = load
        duckPlayer?.updateLoad(load.percentage, reversed: load.reversed, nowSeconds: CACurrentMediaTime())
    }

    func setDuck(kind: RunnerKind = .duck, flipped: Bool, height: CGFloat = 18) -> Bool {
        guard let bundle = DuckAssets.bundle(for: kind) else { return false }
        duckImages = bundle.frames.map { image in
            guard flipped,
                  let context = CGContext(data: nil, width: image.width, height: image.height, bitsPerComponent: 8,
                    bytesPerRow: 0, space: CGColorSpaceCreateDeviceRGB(),
                    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return image }
            context.translateBy(x: CGFloat(image.width), y: 0)
            context.scaleBy(x: -1, y: 1)
            context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
            return context.makeImage() ?? image
        }
        // The duck variants keep their pose; a runner with another loop starts its own player.
        if duckPlayer?.animation != bundle.animation {
            duckPlayer = RunnerAnimationPlayer(animation: bundle.animation)
            setLoad(load)
        }
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }
        removeAllAnimations()
        maskLayer.removeAllAnimations()
        speed = 1
        timeOffset = 0
        beginTime = 0
        maskLayer.speed = 1
        maskLayer.timeOffset = 0
        maskLayer.beginTime = 0
        let image = duckImages[0]
        contentsGravity = .resize
        let size = CGSize(width: CGFloat(image.width) / CGFloat(image.height) * height, height: height)
        frame = CGRect(origin: CGPoint(x: gap, y: max(0, (containerHeight - size.height) / 2)), size: size)
        maskLayer.frame = CGRect(origin: .zero, size: size)
        contents = nil
        maskLayer.contents = nil
        mask = kind.isTemplate ? maskLayer : nil
        if !kind.isTemplate { backgroundColor = nil }
        lastFrame = -1
        lastTick = CACurrentMediaTime()
        drawDuck()
        if duckTimer == nil {
            let target = RunnerAnimationTimerTarget { [weak self] in self?.tickDuck() }
            let timer = Timer(timeInterval: 1.0 / 60, target: target,
                              selector: #selector(RunnerAnimationTimerTarget.fire(_:)),
                              userInfo: nil, repeats: true)
            timer.tolerance = 0.002
            RunLoop.main.add(timer, forMode: .common)
            duckTimer = timer
        }
        return true
    }

    private func tickDuck() {
        let now = CACurrentMediaTime()
        duckPlayer?.advance(seconds: min(0.25, max(0, now - lastTick)))
        lastTick = now
        drawDuck()
    }

    private func drawDuck() {
        guard let player = duckPlayer, player.frameIndex != lastFrame else { return }
        lastFrame = player.frameIndex
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        if mask != nil {
            maskLayer.contents = duckImages[lastFrame]
        } else {
            contents = duckImages[lastFrame]
        }
        CATransaction.commit()
    }

    func setFrames(_ frames: [NSImage], _ isTemplate: Bool) {
        duckTimer?.invalidate()
        duckTimer = nil
        duckPlayer = nil
        duckImages = []
        contentsGravity = .left
        let size = frames.first?.alignmentRect.size ?? .zero
        keyFrameAnimation.values = frames
        keyFrameAnimation.duration = Double(frames.count) / 2.0
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        removeAllAnimations()
        maskLayer.removeAllAnimations()
        contents = nil
        maskLayer.contents = nil
        frame = CGRect(origin: CGPoint(x: gap, y: max(0, (containerHeight - size.height) / 2)), size: size)
        maskLayer.frame = CGRect(origin: .zero, size: size)
        if isTemplate {
            if mask == nil {
                maskLayer.speed = speed
            }
            timeOffset = .zero
            beginTime = .zero
            speed = 1.0
            mask = maskLayer
            maskLayer.add(keyFrameAnimation, forKey: "running")
        } else {
            if mask != nil {
                speed = maskLayer.speed
            }
            mask = nil
            add(keyFrameAnimation, forKey: "running")
        }
        CATransaction.commit()
    }

    func setColor(_ tintColor: CGColor, _ isTemplate: Bool) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        backgroundColor = isTemplate ? tintColor : nil
        CATransaction.commit()
    }

    func setSpeed(_ speed: Float) {
        guard duckPlayer == nil else { return }
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        if mask == nil {
            timeOffset = convertTime(CACurrentMediaTime(), from: nil)
            beginTime = CACurrentMediaTime()
            self.speed = speed
        } else {
            maskLayer.timeOffset = maskLayer.convertTime(CACurrentMediaTime(), from: nil)
            maskLayer.beginTime = CACurrentMediaTime()
            maskLayer.speed = speed
        }
        CATransaction.commit()
    }
}

// The timer is installed only on RunLoop.main. Its target keeps a weak layer
// capture, avoiding a layer/timer retain cycle and cross-actor CALayer transfer.
@MainActor
private final class RunnerAnimationTimerTarget: NSObject {
    private let tick: @MainActor () -> Void
    init(_ tick: @escaping @MainActor () -> Void) {
        self.tick = tick
        super.init()
    }
    @objc func fire(_ timer: Timer) { tick() }
}
