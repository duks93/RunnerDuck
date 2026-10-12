/*
 RunnerKind+Extension.swift
 UserInterface

 Created by Takuto Nakamura on 2026/06/10.
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

import DataSource
extension RunnerKind {
    var localizedName: String {
        switch self {
        case .duck: String(localized: "duck", table: "RunnerNames", bundle: .module)
        case .duckFilled: String(localized: "duck-filled", table: "RunnerNames", bundle: .module)
        case .penguin: String(localized: "penguin", table: "RunnerNames", bundle: .module)
        case .chicken: String(localized: "chicken", table: "RunnerNames", bundle: .module)
        case .dinosaur: String(localized: "dinosaur", table: "RunnerNames", bundle: .module)
        case .owl: String(localized: "owl", table: "RunnerNames", bundle: .module)
        case .cat: String(localized: "cat", table: "RunnerNames", bundle: .module)
        case .dog: String(localized: "dog", table: "RunnerNames", bundle: .module)
        case .pig: String(localized: "pig", table: "RunnerNames", bundle: .module)
        case .sheep: String(localized: "sheep", table: "RunnerNames", bundle: .module)
        case .fox: String(localized: "fox", table: "RunnerNames", bundle: .module)
        case .turtle: String(localized: "turtle", table: "RunnerNames", bundle: .module)
        }
    }
}
