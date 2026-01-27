using Microsoft.Win32.SafeHandles;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// A safe handle wrapper for WFP engine session handles.
/// Ensures the handle is properly closed even if an exception occurs or the handle is not explicitly disposed.
/// </summary>
/// <remarks>
/// This class derives from SafeHandleZeroOrMinusOneIsInvalid because WFP engine handles
/// are pointers where zero and -1 represent invalid handles.
///
/// The handle is automatically closed via FwpmEngineClose0 when:
/// - Dispose() is called
/// - The handle goes out of scope and is garbage collected
/// - The AppDomain is unloaded
/// </remarks>
public sealed class WfpEngineHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Creates a new invalid WFP engine handle.
    /// </summary>
    internal WfpEngineHandle() : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Creates a WFP engine handle wrapping an existing native handle.
    /// </summary>
    /// <param name="existingHandle">The native handle to wrap.</param>
    /// <param name="ownsHandle">True if this SafeHandle should close the handle when disposed.</param>
    internal WfpEngineHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    /// <summary>
    /// Releases the native WFP engine handle by calling FwpmEngineClose0.
    /// </summary>
    /// <returns>True if the handle was successfully released; otherwise, false.</returns>
    /// <remarks>
    /// This method is called automatically by the runtime when the handle is disposed
    /// or finalized. It runs in a constrained execution region (CER) to ensure
    /// the handle is released even in low-resource situations.
    ///
    /// We intentionally ignore the return value from FwpmEngineClose0 here because:
    /// 1. We're in a finalizer context and can't throw
    /// 2. The handle is being released anyway
    /// 3. Logging from a finalizer is problematic
    /// </remarks>
    protected override bool ReleaseHandle()
    {
        if (handle == IntPtr.Zero)
            return true;

        // FwpmEngineClose0 returns ERROR_SUCCESS (0) on success
        // We ignore failures here as we can't throw from a finalizer
        uint result = NativeMethods.FwpmEngineClose0(handle);
        return result == 0;
    }
}
