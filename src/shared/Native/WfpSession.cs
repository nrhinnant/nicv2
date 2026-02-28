namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// Provides high-level operations for WFP engine session management.
/// </summary>
/// <remarks>
/// This class wraps the low-level P/Invoke calls with proper error handling
/// and returns Result types for clean error propagation.
///
/// Usage:
/// <code>
/// var result = WfpSession.OpenEngine();
/// if (result.IsFailure)
/// {
///     Console.WriteLine($"Failed to open engine: {result.Error}");
///     return;
/// }
///
/// using (var handle = result.Value)
/// {
///     // Use the handle for WFP operations...
/// }
/// // Handle is automatically closed when disposed
/// </code>
/// </remarks>
public static class WfpSession
{
    /// <summary>
    /// Opens a session to the local WFP filter engine.
    /// </summary>
    /// <returns>
    /// A Result containing the engine handle on success, or an error on failure.
    /// The caller is responsible for disposing the handle when done.
    /// </returns>
    /// <remarks>
    /// Requires administrator privileges. If called without elevation,
    /// returns an AccessDenied error.
    ///
    /// The returned handle must be disposed to release the engine session.
    /// Use a 'using' statement or call Dispose() explicitly.
    /// </remarks>
    public static Result<WfpEngineHandle> OpenEngine()
    {
        uint result = NativeMethods.FwpmEngineOpen0(
            serverName: null,                           // Local engine
            authnService: NativeMethods.RPC_C_AUTHN_WINNT,
            authIdentity: IntPtr.Zero,
            session: IntPtr.Zero,                       // Default session settings
            out IntPtr rawHandle);

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<WfpEngineHandle>(result, "Failed to open WFP engine");
        }

        // CA2000: False positive - caller receives ownership via Result and is responsible for disposal
#pragma warning disable CA2000
        return Result<WfpEngineHandle>.Success(new WfpEngineHandle(rawHandle, ownsHandle: true));
#pragma warning restore CA2000
    }

    /// <summary>
    /// Explicitly closes a WFP engine session.
    /// </summary>
    /// <param name="handle">The engine handle to close.</param>
    /// <returns>A Result indicating success or failure.</returns>
    /// <remarks>
    /// Normally you should use 'using' or Dispose() on the handle instead of
    /// calling this method directly. This method is provided for cases where
    /// you need to check the close result or handle errors explicitly.
    ///
    /// After calling this method, the handle is invalid and must not be used.
    /// The handle is marked as closed regardless of whether the close succeeded.
    /// </remarks>
    public static Result CloseEngine(WfpEngineHandle handle)
    {
        if (handle == null)
            throw new ArgumentNullException(nameof(handle));

        if (handle.IsInvalid || handle.IsClosed)
            return Result.Success();

        // Get the raw handle before closing
        IntPtr rawHandle = handle.DangerousGetHandle();

        // Mark the SafeHandle as closed so it doesn't try to close again in Dispose
        handle.SetHandleAsInvalid();

        uint result = NativeMethods.FwpmEngineClose0(rawHandle);

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, "Failed to close WFP engine");
        }

        return Result.Success();
    }

    /// <summary>
    /// Checks if WFP engine operations are likely to succeed.
    /// </summary>
    /// <returns>True if an engine session can be opened; false otherwise.</returns>
    /// <remarks>
    /// This attempts to open and immediately close an engine session.
    /// Useful for pre-flight checks before attempting WFP operations.
    ///
    /// Note: This is not a guarantee that subsequent operations will succeed,
    /// as conditions can change between the check and actual use.
    /// </remarks>
    public static bool CanOpenEngine()
    {
        var result = OpenEngine();
        if (result.IsFailure)
            return false;

        result.Value.Dispose();
        return true;
    }
}
