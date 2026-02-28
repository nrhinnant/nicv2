namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// Interface abstracting WFP transaction P/Invoke calls for testability.
/// </summary>
public interface IWfpNativeTransaction
{
    /// <summary>
    /// Begins a transaction on the WFP engine.
    /// </summary>
    /// <param name="engineHandle">The WFP engine handle.</param>
    /// <returns>The raw error code from FwpmTransactionBegin0.</returns>
    uint Begin(IntPtr engineHandle);

    /// <summary>
    /// Commits the current transaction on the WFP engine.
    /// </summary>
    /// <param name="engineHandle">The WFP engine handle.</param>
    /// <returns>The raw error code from FwpmTransactionCommit0.</returns>
    uint Commit(IntPtr engineHandle);

    /// <summary>
    /// Aborts the current transaction on the WFP engine.
    /// </summary>
    /// <param name="engineHandle">The WFP engine handle.</param>
    /// <returns>The raw error code from FwpmTransactionAbort0.</returns>
    uint Abort(IntPtr engineHandle);
}

/// <summary>
/// Default implementation of <see cref="IWfpNativeTransaction"/> using P/Invoke.
/// </summary>
internal sealed class WfpNativeTransaction : IWfpNativeTransaction
{
    /// <summary>
    /// Singleton instance for production use.
    /// </summary>
    public static readonly WfpNativeTransaction Instance = new();

    private WfpNativeTransaction() { }

    public uint Begin(IntPtr engineHandle) =>
        NativeMethods.FwpmTransactionBegin0(engineHandle, 0);

    public uint Commit(IntPtr engineHandle) =>
        NativeMethods.FwpmTransactionCommit0(engineHandle);

    public uint Abort(IntPtr engineHandle) =>
        NativeMethods.FwpmTransactionAbort0(engineHandle);
}

/// <summary>
/// RAII wrapper for WFP engine transactions.
/// Begins transaction on creation, aborts on dispose if not committed.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// var txResult = WfpTransaction.Begin(engineHandle);
/// if (txResult.IsFailure) return txResult.Error;
/// using var transaction = txResult.Value;
///
/// // Do work within transaction...
///
/// var commitResult = transaction.Commit();
/// if (commitResult.IsFailure) return commitResult; // Transaction already aborted
/// </code>
///
/// If Dispose is called without Commit, the transaction is automatically aborted.
/// If Commit fails, Windows aborts the transaction internally.
/// </remarks>
public sealed class WfpTransaction : IDisposable
{
    private readonly IntPtr _engineHandle;
    private readonly IWfpNativeTransaction _nativeTransaction;
    private bool _committed;
    private bool _disposed;

    /// <summary>
    /// Private constructor. Use <see cref="Begin"/> factory method.
    /// </summary>
    private WfpTransaction(IntPtr engineHandle, IWfpNativeTransaction nativeTransaction)
    {
        _engineHandle = engineHandle;
        _nativeTransaction = nativeTransaction;
    }

    /// <summary>
    /// Gets whether this transaction has been committed.
    /// </summary>
    public bool IsCommitted => _committed;

    /// <summary>
    /// Gets whether this transaction has been disposed (committed or aborted).
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets the last error code from a failed operation (0 if no error).
    /// </summary>
    public uint LastErrorCode { get; private set; }

    /// <summary>
    /// Begins a new WFP transaction.
    /// </summary>
    /// <param name="engineHandle">Handle to an open WFP engine session.</param>
    /// <param name="nativeTransaction">The native transaction operations. Use null for default P/Invoke implementation.</param>
    /// <returns>A Result containing the transaction on success, or an error on failure.</returns>
    public static Result<WfpTransaction> Begin(
        IntPtr engineHandle,
        IWfpNativeTransaction? nativeTransaction = null)
    {
        if (engineHandle == IntPtr.Zero)
        {
            return Result<WfpTransaction>.Failure(ErrorCodes.InvalidArgument, "Engine handle cannot be zero.");
        }

        var native = nativeTransaction ?? WfpNativeTransaction.Instance;

        var result = native.Begin(engineHandle);
        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<WfpTransaction>(result, "Failed to begin WFP transaction");
        }

        // CA2000: False positive - caller receives ownership via Result and is responsible for disposal
#pragma warning disable CA2000
        return Result<WfpTransaction>.Success(new WfpTransaction(engineHandle, native));
#pragma warning restore CA2000
    }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <returns>Success if committed, or an error if commit failed.</returns>
    /// <remarks>
    /// If commit fails, Windows automatically aborts the transaction.
    /// After a failed commit, Dispose will not attempt to abort again.
    /// </remarks>
    public Result Commit()
    {
        if (_disposed)
        {
            return Result.Failure(ErrorCodes.InvalidState, "Transaction has already been disposed.");
        }

        if (_committed)
        {
            return Result.Failure(ErrorCodes.InvalidState, "Transaction has already been committed.");
        }

        var result = _nativeTransaction.Commit(_engineHandle);
        if (!WfpErrorTranslator.IsSuccess(result))
        {
            // On commit failure, Windows aborts the transaction automatically
            LastErrorCode = result;
            _committed = false; // Explicitly mark as not committed
            _disposed = true;   // Mark as disposed since Windows aborted it
            return WfpErrorTranslator.ToFailedResult(result, "Failed to commit WFP transaction");
        }

        _committed = true;
        return Result.Success();
    }

    /// <summary>
    /// Disposes the transaction. If not committed, the transaction is aborted.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_committed)
        {
            var result = _nativeTransaction.Abort(_engineHandle);
            if (!WfpErrorTranslator.IsSuccess(result))
            {
                // Store error but don't throw - we're in a dispose context
                LastErrorCode = result;
            }
        }
    }
}
