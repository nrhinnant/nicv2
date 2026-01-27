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

    // ========================================
    // Transaction Management
    // ========================================

    /// <summary>
    /// Begins an explicit transaction within a session.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="flags">Reserved. Must be zero.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmTransactionBegin0(IntPtr engineHandle, uint flags);

    /// <summary>
    /// Commits the current transaction within a session.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmTransactionCommit0(IntPtr engineHandle);

    /// <summary>
    /// Aborts the current transaction within a session.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmTransactionAbort0(IntPtr engineHandle);

    // ========================================
    // Provider Management
    // ========================================

    /// <summary>
    /// Adds a new provider to the system.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="provider">The provider to add.</param>
    /// <param name="sd">Security descriptor (optional, can be null).</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    /// <remarks>
    /// Uses DllImport instead of LibraryImport because FWPM_PROVIDER0 contains
    /// complex types (strings) that source-generated P/Invoke doesn't support.
    /// </remarks>
    [DllImport(Fwpuclnt, SetLastError = false)]
    internal static extern uint FwpmProviderAdd0(
        IntPtr engineHandle,
        in FWPM_PROVIDER0 provider,
        IntPtr sd);

    /// <summary>
    /// Retrieves a provider by its key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the provider.</param>
    /// <param name="provider">Receives a pointer to the provider structure.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_PROVIDER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmProviderGetByKey0(
        IntPtr engineHandle,
        in Guid key,
        out IntPtr provider);

    /// <summary>
    /// Deletes a provider by its key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the provider to delete.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_PROVIDER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmProviderDeleteByKey0(
        IntPtr engineHandle,
        in Guid key);

    /// <summary>
    /// Frees memory returned by WFP API calls (like FwpmProviderGetByKey0).
    /// </summary>
    /// <param name="p">Pointer to the memory to free.</param>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial void FwpmFreeMemory0(ref IntPtr p);

    // ========================================
    // Sublayer Management
    // ========================================

    /// <summary>
    /// Adds a new sublayer to the system.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="subLayer">The sublayer to add.</param>
    /// <param name="sd">Security descriptor (optional, can be null).</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    /// <remarks>
    /// Uses DllImport instead of LibraryImport because FWPM_SUBLAYER0 contains
    /// complex types (strings) that source-generated P/Invoke doesn't support.
    /// </remarks>
    [DllImport(Fwpuclnt, SetLastError = false)]
    internal static extern uint FwpmSubLayerAdd0(
        IntPtr engineHandle,
        in FWPM_SUBLAYER0 subLayer,
        IntPtr sd);

    /// <summary>
    /// Retrieves a sublayer by its key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the sublayer.</param>
    /// <param name="subLayer">Receives a pointer to the sublayer structure.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_SUBLAYER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmSubLayerGetByKey0(
        IntPtr engineHandle,
        in Guid key,
        out IntPtr subLayer);

    /// <summary>
    /// Deletes a sublayer by its key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the sublayer to delete.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_SUBLAYER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmSubLayerDeleteByKey0(
        IntPtr engineHandle,
        in Guid key);

    // ========================================
    // WFP Error Codes
    // These values must align with WfpErrorTranslator.cs and what Windows returns
    // ========================================

    /// <summary>FWP_E_PROVIDER_NOT_FOUND: The provider was not found (0x80320005 from Windows, 0x80320008 in some docs).</summary>
    internal const uint FWP_E_PROVIDER_NOT_FOUND = 0x80320005;

    /// <summary>FWP_E_PROVIDER_NOT_FOUND alternative code used in some Windows versions.</summary>
    internal const uint FWP_E_PROVIDER_NOT_FOUND_ALT = 0x80320008;

    /// <summary>FWP_E_SUBLAYER_NOT_FOUND: The sublayer was not found (0x80320007 from Windows).</summary>
    internal const uint FWP_E_SUBLAYER_NOT_FOUND = 0x80320007;

    /// <summary>FWP_E_SUBLAYER_NOT_FOUND alternative code used in some Windows versions.</summary>
    internal const uint FWP_E_SUBLAYER_NOT_FOUND_ALT = 0x8032000A;

    /// <summary>FWP_E_ALREADY_EXISTS: The object already exists.</summary>
    internal const uint FWP_E_ALREADY_EXISTS = 0x80320009;

    /// <summary>FWP_E_IN_USE: The object is in use and cannot be deleted.</summary>
    internal const uint FWP_E_IN_USE = 0x80320006;
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

/// <summary>
/// Structure for FWPM_PROVIDER0 used in FwpmProviderAdd0.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_PROVIDER0
{
    public Guid providerKey;
    public FWPM_DISPLAY_DATA0 displayData;
    public uint flags;
    public IntPtr providerData;      // FWP_BYTE_BLOB pointer (set to null)
    public IntPtr serviceName;       // Optional service name (set to null)
}

/// <summary>
/// Structure for FWPM_SUBLAYER0 used in FwpmSubLayerAdd0.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_SUBLAYER0
{
    public Guid subLayerKey;
    public FWPM_DISPLAY_DATA0 displayData;
    public uint flags;
    public IntPtr providerKey;       // Pointer to provider GUID (or null)
    public IntPtr providerData;      // FWP_BYTE_BLOB pointer (set to null)
    public ushort weight;            // Priority weight (0-65535)
}

/// <summary>
/// Provider flags for FWPM_PROVIDER0.
/// </summary>
internal static class FwpmProviderFlags
{
    /// <summary>The provider is persistent (survives reboot). Not recommended for user-mode only.</summary>
    public const uint FWPM_PROVIDER_FLAG_PERSISTENT = 0x00000001;

    /// <summary>No special flags (default).</summary>
    public const uint FWPM_PROVIDER_FLAG_NONE = 0x00000000;
}

/// <summary>
/// Sublayer flags for FWPM_SUBLAYER0.
/// </summary>
internal static class FwpmSublayerFlags
{
    /// <summary>The sublayer is persistent (survives reboot). Not recommended for user-mode only.</summary>
    public const uint FWPM_SUBLAYER_FLAG_PERSISTENT = 0x00000001;

    /// <summary>No special flags (default).</summary>
    public const uint FWPM_SUBLAYER_FLAG_NONE = 0x00000000;
}
