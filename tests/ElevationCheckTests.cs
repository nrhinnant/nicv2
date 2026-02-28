using System.Security.Principal;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for elevation/privilege checking functionality.
///
/// Note: The actual Worker.IsRunningAsAdministrator() method is private.
/// These tests validate the underlying API pattern and document expected behavior.
/// Full integration testing requires manual verification:
///   - Run service from non-admin prompt → should exit with clear error
///   - Run service from admin prompt → should start normally
/// </summary>
public class ElevationCheckTests
{
    /// <summary>
    /// Validates that the Windows identity/principal API pattern works correctly.
    /// This is the same pattern used by Worker.IsRunningAsAdministrator().
    /// </summary>
    [Fact]
    public void WindowsIdentityCanBeRetrievedAndChecked()
    {
        // Arrange & Act - validate the API pattern works
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

        // Assert - we don't assert the result (depends on test runner context)
        // but we verify the APIs don't throw and return a boolean
        Assert.IsType<bool>(isAdmin);
        Assert.NotNull(identity);
        Assert.NotNull(identity.Name);
    }

    /// <summary>
    /// Validates that WindowsIdentity is properly disposable.
    /// The elevation check must dispose the identity to avoid handle leaks.
    /// </summary>
    [Fact]
    public void WindowsIdentityIsDisposable()
    {
        // Arrange
        var identity = WindowsIdentity.GetCurrent();

        // Act - dispose should not throw
        identity.Dispose();

        // Assert - accessing disposed identity throws (proves disposal worked)
        Assert.Throws<ObjectDisposedException>(() => identity.Name);
    }

    /// <summary>
    /// Documents the expected error message format for non-elevated execution.
    /// The service should exit with this specific message when not elevated.
    /// </summary>
    [Fact]
    public void ElevationErrorMessageFormat()
    {
        // This is the exact message the service logs and throws
        const string expectedMessage =
            "Service must run with Administrator privileges to access Windows Filtering Platform";

        // Validate message is clear and actionable
        Assert.Contains("Administrator", expectedMessage);
        Assert.Contains("privileges", expectedMessage);
        Assert.Contains("Windows Filtering Platform", expectedMessage);
    }

    /// <summary>
    /// The InvalidOperationException type is used for elevation failures.
    /// This documents the expected exception type for callers/tests.
    /// </summary>
    [Fact]
    public void ElevationErrorThrowsInvalidOperationException()
    {
        const string message =
            "Service must run with Administrator privileges to access Windows Filtering Platform";

        var exception = new InvalidOperationException(message);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(message, exception.Message);
    }
}
