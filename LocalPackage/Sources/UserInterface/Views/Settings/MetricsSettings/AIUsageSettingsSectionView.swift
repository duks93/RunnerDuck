/*
 AIUsageSettingsSectionView.swift
 UserInterface

 Created by duks93 on 2026/09/25.
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

import Model
import SwiftUI

struct AIUsageSettingsSectionView: View {
    @State var store: AIUsageSettings

    var body: some View {
        Section {
            ForEach(store.sourceStates) { sourceState in
                Toggle(isOn: Binding<Bool>(
                    get: { sourceState.isEnabled },
                    asyncSet: { await store.send(.sourceToggleSwitched(sourceState.source, $0)) }
                )) {
                    Text(verbatim: sourceState.source.displayName)
                    Text(sourceState.isDetected ? "aiUsageDetected" : "aiUsageNotDetected", bundle: .module)
                }
            }
        } header: {
            Text("aiUsage", bundle: .module)
        } footer: {
            Text("aiUsageSettingsFooter", bundle: .module)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .task {
            await store.send(.viewAppeared)
        }
    }
}
