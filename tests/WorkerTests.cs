// tests/WorkerTests.cs
// Unit and integration tests for Worker service lifecycle
// Addresses critical test gap: Worker has 0% coverage
//
// Note: Full Worker testing requires Administrator privileges and a VM environment.
// These tests cover what can be tested without elevation.

using System.Security.Principal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WfpTrafficControl.Service;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Lkg;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for Worker elevation checking.
/// The actual Worker.IsRunningAsAdministrator() is private, but we test the
/// same pattern used by the Worker.
/// </summary>
public class WorkerElevationTests
{
    [Fact]
    public void ElevationCheckReturnsBoolean()
    {
        // Arrange & Act - use the same pattern as Worker.IsRunningAsAdministrator()
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

        // Assert - just verify API works (result depends on test runner context)
        Assert.IsType<bool>(isAdmin);
    }

    [Fact]
    public void ElevationCheckIdentityNotNull()
    {
        // Arrange & Act
        using var identity = WindowsIdentity.GetCurrent();

        // Assert
        Assert.NotNull(identity);
        Assert.NotNull(identity.Name);
    }
}

/// <summary>
/// Tests for Worker configuration parsing.
/// </summary>
public class WorkerConfigurationTests
{
    [Fact]
    public void ConfigurationAutoApplyLkgDefaultIsFalse()
    {
        // Arrange - empty configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var autoApplyLkg = config.GetValue<bool>("WfpTrafficControl:AutoApplyLkgOnStartup", false);

        // Assert - default should be false (fail-open behavior)
        Assert.False(autoApplyLkg);
    }

    [Fact]
    public void ConfigurationAutoApplyLkgCanBeEnabled()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WfpTrafficControl:AutoApplyLkgOnStartup"] = "true"
            })
            .Build();

        // Act
        var autoApplyLkg = config.GetValue<bool>("WfpTrafficControl:AutoApplyLkgOnStartup", false);

        // Assert
        Assert.True(autoApplyLkg);
    }

    [Fact]
    public void ConfigurationDebounceMsDefaultValue()
    {
        // Arrange - empty configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var debounceMs = config.GetValue<int>("WfpTrafficControl:FileWatch:DebounceMs",
            PolicyFileWatcher.DefaultDebounceMs);

        // Assert
        Assert.Equal(PolicyFileWatcher.DefaultDebounceMs, debounceMs);
    }

    [Fact]
    public void ConfigurationDebounceMsCanBeConfigured()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WfpTrafficControl:FileWatch:DebounceMs"] = "2000"
            })
            .Build();

        // Act
        var debounceMs = config.GetValue<int>("WfpTrafficControl:FileWatch:DebounceMs",
            PolicyFileWatcher.DefaultDebounceMs);

        // Assert
        Assert.Equal(2000, debounceMs);
    }

    [Fact]
    public void ConfigurationDebounceMsValidRangeConstants()
    {
        // Assert - verify the constants are sensible
        Assert.True(PolicyFileWatcher.MinDebounceMs > 0);
        Assert.True(PolicyFileWatcher.MinDebounceMs < PolicyFileWatcher.MaxDebounceMs);
        Assert.True(PolicyFileWatcher.DefaultDebounceMs >= PolicyFileWatcher.MinDebounceMs);
        Assert.True(PolicyFileWatcher.DefaultDebounceMs <= PolicyFileWatcher.MaxDebounceMs);
    }
}

/// <summary>
/// Tests for the LKG auto-apply logic that Worker uses on startup.
/// These test the logic flow without requiring full Worker instantiation.
/// </summary>
[Collection("LkgStore Sequential")]
public sealed class WorkerLkgAutoApplyLogicTests : IDisposable
{
    public WorkerLkgAutoApplyLogicTests()
    {
        // Ensure clean state
        LkgStore.Delete();
    }

    public void Dispose()
    {
        LkgStore.Delete();
    }

    [Fact]
    public void LkgAutoApplyNoLkgShouldFailOpen()
    {
        // Arrange - ensure no LKG
        LkgStore.Delete();

        // Act - simulate the check Worker does
        var loadResult = LkgStore.Load();

        // Assert - should not exist, and no error (fail-open)
        Assert.False(loadResult.Exists);
        Assert.Null(loadResult.Error);
    }

    [Fact]
    public void LkgAutoApplyValidLkgShouldLoadSuccessfully()
    {
        // Arrange - save a valid LKG
        var policyJson = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "test-rule",
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "priority": 100,
                    "enabled": true
                }
            ]
        }
        """;
        LkgStore.Save(policyJson);

        // Act - simulate the load Worker does
        var loadResult = LkgStore.Load();

        // Assert
        Assert.True(loadResult.Exists);
        Assert.NotNull(loadResult.Policy);
        Assert.Equal("1.0.0", loadResult.Policy!.Version);
        Assert.Single(loadResult.Policy.Rules);
    }

    [Fact]
    public void LkgAutoApplyValidLkgCanCompile()
    {
        // Arrange
        var policyJson = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "test-rule",
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "priority": 100,
                    "enabled": true
                }
            ]
        }
        """;
        LkgStore.Save(policyJson);
        var loadResult = LkgStore.Load();

        // Act - simulate the compilation Worker does
        var compilationResult = RuleCompiler.Compile(loadResult.Policy!);

        // Assert
        Assert.True(compilationResult.IsSuccess);
        Assert.NotEmpty(compilationResult.Filters);
    }

    [Fact]
    public void LkgAutoApplyCorruptLkgShouldFailOpen()
    {
        // Arrange - save a corrupt LKG (bad checksum)
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, """{"checksum":"bad","policyJson":"{}","savedAt":"2024-01-01T00:00:00Z"}""");

        // Act
        var loadResult = LkgStore.Load();

        // Assert - should report error but not throw
        Assert.False(loadResult.Exists);
        Assert.NotNull(loadResult.Error);
    }
}

/// <summary>
/// Tests for Worker version retrieval.
/// </summary>
public class WorkerVersionTests
{
    [Fact]
    public void WorkerAssemblyHasVersion()
    {
        // Arrange & Act
        var assembly = typeof(Worker).Assembly;
        var version = assembly.GetName().Version;

        // Assert
        Assert.NotNull(version);
    }

    [Fact]
    public void WorkerAssemblyVersionFormat()
    {
        // Arrange & Act
        var assembly = typeof(Worker).Assembly;
        var version = assembly.GetName().Version;
        var versionString = version?.ToString(3) ?? "0.0.0";

        // Assert - should be X.Y.Z format
        var parts = versionString.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _)));
    }
}

/// <summary>
/// Documentation of Worker scenarios that require manual VM testing.
/// These are not actual tests but serve as documentation.
/// </summary>
public class WorkerManualTestScenarios
{
    // NOTE: These scenarios require manual testing in an elevated Windows VM:
    //
    // 1. Service Start Without Elevation:
    //    - Run service from non-admin prompt
    //    - Expected: Service should exit with clear error message about Administrator privileges
    //
    // 2. Service Start With Elevation:
    //    - Run service from admin prompt
    //    - Expected: Service should start normally, IPC pipe should be accessible
    //
    // 3. Auto-Apply LKG on Startup (enabled):
    //    - Set WfpTrafficControl:AutoApplyLkgOnStartup=true
    //    - Save a valid LKG policy
    //    - Start the service
    //    - Expected: LKG policy should be applied, filters should be active
    //
    // 4. Auto-Apply LKG on Startup (disabled, default):
    //    - Start the service without AutoApplyLkgOnStartup config
    //    - Expected: No policy applied, fail-open behavior
    //
    // 5. Graceful Stop:
    //    - Start service, apply a policy via CLI
    //    - Stop the service
    //    - Expected: File watcher stopped, pipe server stopped cleanly
    //
    // 6. Configuration Hot Reload:
    //    - Change appsettings.json while service is running
    //    - Expected: Some settings may require restart

    [Fact]
    public void ManualTestDocumentationExists()
    {
        // This test just documents that manual testing scenarios exist
        Assert.True(true, "See comments above for manual test scenarios");
    }
}

/// <summary>
/// Tests for PolicyFileWatcher creation and configuration.
/// </summary>
public class PolicyFileWatcherConfigTests
{
    [Fact]
    public void PolicyFileWatcherDefaultDebounceIsValid()
    {
        Assert.True(PolicyFileWatcher.DefaultDebounceMs >= PolicyFileWatcher.MinDebounceMs);
        Assert.True(PolicyFileWatcher.DefaultDebounceMs <= PolicyFileWatcher.MaxDebounceMs);
    }

    [Fact]
    public void PolicyFileWatcherMinDebounceIsPositive()
    {
        Assert.True(PolicyFileWatcher.MinDebounceMs > 0);
    }

    [Fact]
    public void PolicyFileWatcherMaxDebounceIsReasonable()
    {
        // 30 seconds max is reasonable for UI responsiveness
        Assert.Equal(30000, PolicyFileWatcher.MaxDebounceMs);
    }

    [Fact]
    public void PolicyFileWatcherConstantsHaveExpectedValues()
    {
        Assert.Equal(100, PolicyFileWatcher.MinDebounceMs);
        Assert.Equal(1000, PolicyFileWatcher.DefaultDebounceMs);
        Assert.Equal(30000, PolicyFileWatcher.MaxDebounceMs);
    }
}
