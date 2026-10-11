import AllocatedUnfairLock
import Foundation
import ServiceManagement
import Testing

@testable import DataSource
@testable import Model

struct LaunchAtLoginServiceTests {
    @Test(arguments: [SMAppService.Status.notRegistered, .notFound])
    func enableByDefault_registers_once_and_preserves_later_system_disable(initialStatus: SMAppService.Status) throws {
        let initialized = AllocatedUnfairLock(initialState: false)
        let registered = AllocatedUnfairLock(initialState: false)
        let calls = AllocatedUnfairLock(initialState: 0)
        let dependencies = AppDependencies.testDependencies(
            smAppServiceClient: testDependency(of: SMAppServiceClient.self) {
                $0.status = { registered.withLock(\.self) ? .enabled : initialStatus }
                $0.register = {
                    registered.withLock { $0 = true }
                    calls.withLock { $0 += 1 }
                }
            },
            userDefaultsClient: defaults(initialized)
        )
        try LaunchAtLoginService(dependencies).enableByDefault().get()
        #expect(registered.withLock { $0 })
        registered.withLock { $0 = false }
        try LaunchAtLoginService(dependencies).enableByDefault().get()
        #expect(!registered.withLock { $0 })
        #expect(calls.withLock(\.self) == 1)
    }

    @Test(arguments: [SMAppService.Status.enabled, .requiresApproval])
    func enableByDefault_leaves_existing_system_status_unchanged(status: SMAppService.Status) throws {
        let initialized = AllocatedUnfairLock(initialState: false)
        let calls = AllocatedUnfairLock(initialState: 0)
        let sut = LaunchAtLoginService(.testDependencies(
            smAppServiceClient: testDependency(of: SMAppServiceClient.self) {
                $0.status = { status }
                $0.register = { calls.withLock { $0 += 1 } }
            },
            userDefaultsClient: defaults(initialized)
        ))
        try sut.enableByDefault().get()
        #expect(calls.withLock(\.self) == 0)
        #expect(initialized.withLock { $0 })
    }

    @Test
    func enableByDefault_reports_failure_without_retrying_on_next_launch() throws {
        let initialized = AllocatedUnfairLock(initialState: false)
        let calls = AllocatedUnfairLock(initialState: 0)
        let sut = LaunchAtLoginService(.testDependencies(
            smAppServiceClient: testDependency(of: SMAppServiceClient.self) {
                $0.status = { .notRegistered }
                $0.register = {
                    calls.withLock { $0 += 1 }
                    throw URLError(.unknown)
                }
            },
            userDefaultsClient: defaults(initialized)
        ))
        #expect(throws: LaunchAtLoginRepository.OperationError.switchFailed(false)) {
            try sut.enableByDefault().get()
        }
        try sut.enableByDefault().get()
        #expect(calls.withLock(\.self) == 1)
    }

    @MainActor @Test
    func manual_disable_before_default_initialization_is_preserved() async throws {
        let initialized = AllocatedUnfairLock(initialState: false)
        let calls = AllocatedUnfairLock(initialState: 0)
        let dependencies = AppDependencies.testDependencies(
            smAppServiceClient: testDependency(of: SMAppServiceClient.self) {
                $0.status = { .notRegistered }
                $0.register = { calls.withLock { $0 += 1 } }
            },
            userDefaultsClient: defaults(initialized)
        )
        let settings = GeneralSettings(dependencies)
        await settings.send(.launchAtLoginToggleSwitched(false))
        try LaunchAtLoginService(dependencies).enableByDefault().get()
        #expect(!settings.launchesAtLogin)
        #expect(calls.withLock(\.self) == 0)
        #expect(initialized.withLock { $0 })
    }

    private func defaults(_ initialized: AllocatedUnfairLock<Bool>) -> UserDefaultsClient {
        testDependency(of: UserDefaultsClient.self) {
            $0.bool = { key in
                key == "LAUNCH_AT_LOGIN_INITIALIZED" && initialized.withLock(\.self)
            }
            $0.set = { value, key in
                if key == "LAUNCH_AT_LOGIN_INITIALIZED" {
                    let flag = value as? Bool ?? false
                    initialized.withLock { $0 = flag }
                }
            }
        }
    }
}
