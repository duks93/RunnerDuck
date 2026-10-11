import AllocatedUnfairLock
import Testing

@testable import DataSource

struct RunnerSizeRepositoryTests {
    @Test(arguments: RunnerSize.allCases)
    func runnerSize_persists_and_is_restored(size: RunnerSize) {
        let stored = AllocatedUnfairLock<Int>(initialState: 0)
        let client = testDependency(of: UserDefaultsClient.self) {
            $0.integer = { key in key == "RUNNER_SIZE" ? stored.withLock(\.self) : 0 }
            $0.set = { value, key in
                guard key == "RUNNER_SIZE", let value = value as? Int else { return }
                stored.withLock { $0 = value }
            }
        }
        let repository = UserDefaultsRepository(client)
        repository.runnerSize = size
        #expect(UserDefaultsRepository(client).runnerSize == size)
    }

    @Test(arguments: [0, -1, 999])
    func runnerSize_missing_or_invalid_uses_standard(value: Int) {
        let client = testDependency(of: UserDefaultsClient.self) {
            $0.integer = { _ in value }
        }
        #expect(UserDefaultsRepository(client).runnerSize == .standard)
    }
}
