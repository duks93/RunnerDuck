/*
 BackgroundLayer.swift
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
import CoreImage.CIFilterBuiltins

final class BackgroundLayer: CALayer {
    private let gap: CGFloat
    private let containerHeight: CGFloat

    init(gap: CGFloat, containerHeight: CGFloat = 22) {
        self.gap = gap
        self.containerHeight = containerHeight
        super.init()
        contentsGravity = CALayerContentsGravity.left
        masksToBounds = true
        contentsScale = 2.0
        let filter = CIFilter.sourceOutCompositing()
        let rect = CGRect(x: 0, y: 0, width: 600, height: containerHeight)
        filter.backgroundImage = CIImage(color: CIColor.blue).cropped(to: rect)
        backgroundFilters = [filter]
    }

    override init(layer: Any) {
        guard let backgroundLayer = layer as? BackgroundLayer else {
            preconditionFailure("Expected a BackgroundLayer presentation source.")
        }
        gap = backgroundLayer.gap
        containerHeight = backgroundLayer.containerHeight
        super.init(layer: layer)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func setSize(_ size: CGSize) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        frame = CGRect(origin: CGPoint(x: gap, y: max(0, (containerHeight - size.height) / 2)), size: size)
        CATransaction.commit()
    }
}
