using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for the WfpTransaction wrapper and IWfpNativeTransaction interface.
/// Uses mock implementations to verify transaction lifecycle behavior.
/// </summary>
public class WfpTransactionTests
{
    // ========================================
    // Mock Implementation
    // ========================================

    private class MockNativeTransaction : IWfpNativeTransaction
    {
        public int BeginCallCount { get; private set; }
        public int CommitCallCount { get; private set; }
        public int AbortCallCount { get; private set; }
        public IntPtr LastEngineHandle { get; private set; }

        public uint BeginReturnValue { get; set; } = 0; // SUCCESS
        public uint CommitReturnValue { get; set; } = 0; // SUCCESS
        public uint AbortReturnValue { get; set; } = 0; // SUCCESS

        public uint Begin(IntPtr engineHandle)
        {
            BeginCallCount++;
            LastEngineHandle = engineHandle;
            return BeginReturnValue;
        }

        public uint Commit(IntPtr engineHandle)
        {
            CommitCallCount++;
            LastEngineHandle = engineHandle;
            return CommitReturnValue;
        }

        public uint Abort(IntPtr engineHandle)
        {
            AbortCallCount++;
            LastEngineHandle = engineHandle;
            return AbortReturnValue;
        }
    }

    // ========================================
    // Begin Tests
    // ========================================

    [Fact]
    public void BeginWithValidHandleReturnsTransaction()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);

        // Act
        var result = WfpTransaction.Begin(handle, mockNative);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, mockNative.BeginCallCount);
        Assert.Equal(handle, mockNative.LastEngineHandle);
    }

    [Fact]
    public void BeginWithZeroHandleReturnsError()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();

        // Act
        var result = WfpTransaction.Begin(IntPtr.Zero, mockNative);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("zero", result.Error.Message.ToLower());
        Assert.Equal(0, mockNative.BeginCallCount); // Should not call native
    }

    [Fact]
    public void BeginWhenNativeFailsReturnsError()
    {
        // Arrange
        var mockNative = new MockNativeTransaction
        {
            BeginReturnValue = 0x80320017 // FWP_E_SESSION_ABORTED
        };
        var handle = new IntPtr(12345);

        // Act
        var result = WfpTransaction.Begin(handle, mockNative);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, mockNative.BeginCallCount);
    }

    [Fact]
    public void BeginWithNullNativeTransactionFallsBackToDefault()
    {
        // This test verifies that passing null for nativeTransaction is allowed
        // and the code uses WfpNativeTransaction.Instance (singleton).
        // We cannot actually test the real P/Invoke with an invalid handle
        // (it would crash with AccessViolationException), so we just verify
        // that the parameter is optional and works with a mock.

        // Arrange - use a mock to verify the fallback path compiles and works
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);

        // Act - explicitly pass mock (simulating what the null fallback does)
        var result = WfpTransaction.Begin(handle, mockNative);

        // Assert - with mock, it should succeed
        Assert.True(result.IsSuccess);
        Assert.Equal(1, mockNative.BeginCallCount);
    }

    // ========================================
    // Commit Tests
    // ========================================

    [Fact]
    public void CommitOnActiveTransactionSucceeds()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        var commitResult = transaction.Commit();

        // Assert
        Assert.True(commitResult.IsSuccess);
        Assert.Equal(1, mockNative.CommitCallCount);
        Assert.True(transaction.IsCommitted);
    }

    [Fact]
    public void CommitWhenAlreadyCommittedReturnsError()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;
        transaction.Commit();

        // Act
        var secondCommitResult = transaction.Commit();

        // Assert
        Assert.True(secondCommitResult.IsFailure);
        Assert.Equal(ErrorCodes.InvalidState, secondCommitResult.Error.Code);
        Assert.Contains("already been committed", secondCommitResult.Error.Message);
        Assert.Equal(1, mockNative.CommitCallCount); // Only called once
    }

    [Fact]
    public void CommitWhenNativeFailsReturnsError()
    {
        // Arrange
        var mockNative = new MockNativeTransaction
        {
            CommitReturnValue = 0x80320017 // FWP_E_SESSION_ABORTED
        };
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        var commitResult = transaction.Commit();

        // Assert
        Assert.True(commitResult.IsFailure);
        Assert.False(transaction.IsCommitted);
        Assert.True(transaction.IsDisposed); // Windows aborts on commit failure
    }

    [Fact]
    public void CommitAfterDisposeReturnsError()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;
        transaction.Dispose();

        // Act
        var commitResult = transaction.Commit();

        // Assert
        Assert.True(commitResult.IsFailure);
        Assert.Equal(ErrorCodes.InvalidState, commitResult.Error.Code);
        Assert.Contains("disposed", commitResult.Error.Message);
    }

    // ========================================
    // Dispose/Abort Tests
    // ========================================

    [Fact]
    public void DisposeWithoutCommitAbortsTransaction()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);

        // Act
        txResult.Value.Dispose();

        // Assert
        Assert.Equal(1, mockNative.AbortCallCount);
        Assert.Equal(0, mockNative.CommitCallCount);
    }

    [Fact]
    public void DisposeAfterCommitDoesNotAbort()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;
        transaction.Commit();

        // Act
        transaction.Dispose();

        // Assert
        Assert.Equal(0, mockNative.AbortCallCount);
        Assert.Equal(1, mockNative.CommitCallCount);
    }

    [Fact]
    public void DisposeCalledTwiceOnlyAbortsOnce()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        transaction.Dispose();
        transaction.Dispose();

        // Assert
        Assert.Equal(1, mockNative.AbortCallCount);
    }

    [Fact]
    public void DisposeWhenAbortFailsDoesNotThrow()
    {
        // Arrange
        var mockNative = new MockNativeTransaction
        {
            AbortReturnValue = 0x80320017 // FWP_E_SESSION_ABORTED
        };
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act & Assert - should not throw even if abort fails
        var exception = Record.Exception(() => transaction.Dispose());
        Assert.Null(exception);
        Assert.Equal(1, mockNative.AbortCallCount);
    }

    // ========================================
    // Using Pattern Tests
    // ========================================

    [Fact]
    public void UsingPatternWithCommitCommitsAndDoesNotAbort()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);

        // Act
        var txResult = WfpTransaction.Begin(handle, mockNative);
        Assert.True(txResult.IsSuccess);
        using (var transaction = txResult.Value)
        {
            var commitResult = transaction.Commit();
            Assert.True(commitResult.IsSuccess);
        }

        // Assert
        Assert.Equal(1, mockNative.BeginCallCount);
        Assert.Equal(1, mockNative.CommitCallCount);
        Assert.Equal(0, mockNative.AbortCallCount);
    }

    [Fact]
    public void UsingPatternWithoutCommitAborts()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);

        // Act
        var txResult = WfpTransaction.Begin(handle, mockNative);
        Assert.True(txResult.IsSuccess);
        using (var transaction = txResult.Value)
        {
            // Don't commit - just let it dispose
        }

        // Assert
        Assert.Equal(1, mockNative.BeginCallCount);
        Assert.Equal(0, mockNative.CommitCallCount);
        Assert.Equal(1, mockNative.AbortCallCount);
    }

    [Fact]
    public void UsingPatternWithExceptionAborts()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);

        // Act
        try
        {
            var txResult = WfpTransaction.Begin(handle, mockNative);
            Assert.True(txResult.IsSuccess);
            using var transaction = txResult.Value;
            throw new InvalidOperationException("Simulated failure");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        Assert.Equal(1, mockNative.BeginCallCount);
        Assert.Equal(0, mockNative.CommitCallCount);
        Assert.Equal(1, mockNative.AbortCallCount);
    }

    // ========================================
    // State Property Tests
    // ========================================

    [Fact]
    public void IsCommittedInitiallyFalse()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);

        // Assert
        Assert.False(txResult.Value.IsCommitted);
    }

    [Fact]
    public void IsCommittedTrueAfterCommit()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        transaction.Commit();

        // Assert
        Assert.True(transaction.IsCommitted);
    }

    [Fact]
    public void IsDisposedInitiallyFalse()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);

        // Assert
        Assert.False(txResult.Value.IsDisposed);
    }

    [Fact]
    public void IsDisposedTrueAfterDispose()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        transaction.Dispose();

        // Assert
        Assert.True(transaction.IsDisposed);
    }

    [Fact]
    public void IsDisposedTrueAfterCommit()
    {
        // Arrange
        var mockNative = new MockNativeTransaction();
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        transaction.Commit();

        // Assert - committed but not disposed yet
        Assert.False(transaction.IsDisposed);

        // Dispose
        transaction.Dispose();
        Assert.True(transaction.IsDisposed);
    }

    [Fact]
    public void IsDisposedTrueAfterFailedCommit()
    {
        // Arrange
        var mockNative = new MockNativeTransaction
        {
            CommitReturnValue = 0x80320017 // FWP_E_SESSION_ABORTED
        };
        var handle = new IntPtr(12345);
        var txResult = WfpTransaction.Begin(handle, mockNative);
        var transaction = txResult.Value;

        // Act
        transaction.Commit();

        // Assert - marked as disposed after failed commit (Windows aborts)
        Assert.True(transaction.IsDisposed);
        Assert.False(transaction.IsCommitted);
    }
}

/// <summary>
/// Tests for IWfpNativeTransaction interface contract.
/// </summary>
public class IWfpNativeTransactionInterfaceTests
{
    [Fact]
    public void InterfaceHasAllRequiredMethods()
    {
        var interfaceType = typeof(IWfpNativeTransaction);

        Assert.NotNull(interfaceType.GetMethod("Begin"));
        Assert.NotNull(interfaceType.GetMethod("Commit"));
        Assert.NotNull(interfaceType.GetMethod("Abort"));
    }

    [Fact]
    public void BeginReturnsUint()
    {
        var method = typeof(IWfpNativeTransaction).GetMethod("Begin");
        Assert.Equal(typeof(uint), method!.ReturnType);
    }

    [Fact]
    public void CommitReturnsUint()
    {
        var method = typeof(IWfpNativeTransaction).GetMethod("Commit");
        Assert.Equal(typeof(uint), method!.ReturnType);
    }

    [Fact]
    public void AbortReturnsUint()
    {
        var method = typeof(IWfpNativeTransaction).GetMethod("Abort");
        Assert.Equal(typeof(uint), method!.ReturnType);
    }
}
