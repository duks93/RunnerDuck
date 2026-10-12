/*
 RunnerKind.swift
 DataSource

 Created by Takuto Nakamura on 2026/05/09.
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

public enum RunnerKind: String, Sendable, Identifiable, CaseIterable {
    case duck
    case duckFilled = "duck-filled"
    case penguin
    case chicken
    case dinosaur
    case owl
    case cat
    case dog
    case pig
    case sheep
    case fox
    case turtle
    public var id: String { rawValue }
    public var isTemplate: Bool { self == .duck }
    var numberOfResources: Int {
        switch self {
        case .duck, .duckFilled, .owl, .pig, .turtle: 8
        case .penguin, .chicken, .sheep: 10
        case .dinosaur, .cat, .dog, .fox: 12
        }
    }
    var frameOrder: FrameOrder { .ascending(numberOfResources) }
}
