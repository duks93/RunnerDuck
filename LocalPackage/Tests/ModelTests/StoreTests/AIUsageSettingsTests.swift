import Foundation
import Testing

@testable import DataSource
@testable import Model

struct AIUsageSettingsTests {
    @MainActor @Test
    func send_sourceToggleSwitched_persists_override_and_reloads_states() async {
        let storage = UserDefaultsClient.storage()
        let sut = AIUsageSettings(.testDependencies(userDefaultsClient: storage.client))
        await sut.send(.viewAppeared)
        #expect(sut.sourceStates.allSatisfy { !$0.isEnabled })
        await sut.send(.sourceToggleSwitched(.grok, true))
        #expect(sut.sourceStates.first { $0.source == .grok } == AIUsageSourceState(
            source: .grok,
            isDetected: false,
            isEnabled: true
        ))
    }
}
