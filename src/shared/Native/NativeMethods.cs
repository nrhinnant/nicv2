using System.Runtime.InteropServices;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// P/Invoke declarations for Windows Filtering Platform (WFP) APIs.
/// These are the minimal set needed for engine session management.
/// </summary>
internal static partial class NativeMethods
{
    private const string Fwpuclnt = "fwpuclnt.dll";

    /// <summary>
    /// Authentication service type for WFP sessions.
    /// </summary>
    internal const uint RPC_C_AUTHN_DEFAULT = 0xFFFFFFFF;
    internal const uint RPC_C_AUTHN_WINNT = 10;

    /// <summary>
    /// Session flags for FwpmEngineOpen0.
    /// </summary>
    internal const uint FWPM_SESSION_FLAG_DYNAMIC = 0x00000001;

    /// <summary>
    /// Opens a session to the local WFP filter engine.
    /// </summary>
    /// <param name="serverName">Must be null for local engine.</param>
    /// <param name="authnService">Authentication service type. Use RPC_C_AUTHN_WINNT.</param>
    /// <param name="authIdentity">Must be null.</param>
    /// <param name="session">Optional session settings. Can be null for default.</param>
    /// <param name="engineHandle">Receives the engine handle on success.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    /// <remarks>
    /// Documentation: https://docs.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmengineopen0
    /// The caller must close the handle with FwpmEngineClose0 when done.
    /// </remarks>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmEngineOpen0(
        [MarshalAs(UnmanagedType.LPWStr)] string? serverName,
        uint authnService,
        IntPtr authIdentity,
        IntPtr session,
        out IntPtr engineHandle);

    /// <summary>
    /// Closes a session to the WFP filter engine.
    /// </summary>
    /// <param name="engineHandle">The engine handle to close.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    /// <remarks>
    /// Documentation: https://docs.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmengineclose0
    /// After this call, the handle is invalid and must not be used.
    /// </remarks>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmEngineClose0(IntPtr engineHandle);
}

/// <summary>
/// Structure for FWPM_SESSION0 used in FwpmEngineOpen0.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_SESSION0
{
    public Guid sessionKey;
    public FWPM_DISPLAY_DATA0 displayData;
    public uint flags;
    public uint txnWaitTimeoutInMSec;
    public uint processId;
    public IntPtr sid;
    public IntPtr username;
    [MarshalAs(UnmanagedType.Bool)]
    public bool kernelMode;
}

/// <summary>
/// Structure for display data (name and description) used in WFP objects.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_DISPLAY_DATA0
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? name;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? description;
}
