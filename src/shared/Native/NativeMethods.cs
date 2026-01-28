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

    /// <summary>
    /// Retrieves an application identifier (device path) from a file name.
    /// Used to match processes in WFP filters.
    /// </summary>
    /// <param name="fileName">The DOS-style file name (e.g., C:\Windows\System32\notepad.exe).</param>
    /// <param name="appId">Receives a pointer to an FWP_BYTE_BLOB containing the app ID.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    /// <remarks>
    /// The caller must free the returned appId blob using FwpmFreeMemory0.
    /// </remarks>
    [LibraryImport(Fwpuclnt, SetLastError = false, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint FwpmGetAppIdFromFileName0(
        string fileName,
        out IntPtr appId);

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

    /// <summary>FWP_E_FILTER_NOT_FOUND: The filter was not found.</summary>
    internal const uint FWP_E_FILTER_NOT_FOUND = 0x80320003;

    // ========================================
    // Filter Management
    // ========================================

    /// <summary>
    /// Adds a new filter to the system.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="filter">The filter to add.</param>
    /// <param name="sd">Security descriptor (optional, can be null).</param>
    /// <param name="id">Receives the runtime filter ID on success.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [DllImport(Fwpuclnt, SetLastError = false)]
    internal static extern uint FwpmFilterAdd0(
        IntPtr engineHandle,
        in FWPM_FILTER0 filter,
        IntPtr sd,
        out ulong id);

    /// <summary>
    /// Deletes a filter by its runtime ID.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="id">The runtime ID of the filter to delete.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_FILTER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmFilterDeleteById0(IntPtr engineHandle, ulong id);

    /// <summary>
    /// Deletes a filter by its GUID key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the filter to delete.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_FILTER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmFilterDeleteByKey0(
        IntPtr engineHandle,
        in Guid key);

    /// <summary>
    /// Retrieves a filter by its GUID key.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="key">The GUID key of the filter.</param>
    /// <param name="filter">Receives a pointer to the filter structure.</param>
    /// <returns>ERROR_SUCCESS (0) on success, FWP_E_FILTER_NOT_FOUND if not found, or an error code.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmFilterGetByKey0(
        IntPtr engineHandle,
        in Guid key,
        out IntPtr filter);

    // ========================================
    // Filter Enumeration
    // ========================================

    /// <summary>
    /// Creates a handle used to enumerate filters.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="enumTemplate">Template used to filter the results. Can be null to enumerate all.</param>
    /// <param name="enumHandle">Receives the enumeration handle.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [DllImport(Fwpuclnt, SetLastError = false)]
    internal static extern uint FwpmFilterCreateEnumHandle0(
        IntPtr engineHandle,
        IntPtr enumTemplate,
        out IntPtr enumHandle);

    /// <summary>
    /// Returns the next batch of filters from the enumeration.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="enumHandle">Handle returned by FwpmFilterCreateEnumHandle0.</param>
    /// <param name="numEntriesRequested">Maximum number of entries to return.</param>
    /// <param name="entries">Receives an array of FWPM_FILTER0 pointers.</param>
    /// <param name="numEntriesReturned">Number of entries actually returned.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmFilterEnum0(
        IntPtr engineHandle,
        IntPtr enumHandle,
        uint numEntriesRequested,
        out IntPtr entries,
        out uint numEntriesReturned);

    /// <summary>
    /// Frees a handle returned by FwpmFilterCreateEnumHandle0.
    /// </summary>
    /// <param name="engineHandle">Handle for an open session to the filter engine.</param>
    /// <param name="enumHandle">Handle to destroy.</param>
    /// <returns>ERROR_SUCCESS (0) on success, or an error code on failure.</returns>
    [LibraryImport(Fwpuclnt, SetLastError = false)]
    internal static partial uint FwpmFilterDestroyEnumHandle0(
        IntPtr engineHandle,
        IntPtr enumHandle);
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

// ========================================
// WFP Layer GUIDs
// ========================================

/// <summary>
/// WFP layer identifiers for filtering.
/// </summary>
internal static class WfpLayerGuids
{
    /// <summary>
    /// FWPM_LAYER_ALE_AUTH_CONNECT_V4: Filters outbound IPv4 connection attempts.
    /// This layer is evaluated when an application initiates an outbound TCP connection
    /// or sends the first UDP packet to a remote endpoint.
    /// </summary>
    public static readonly Guid FWPM_LAYER_ALE_AUTH_CONNECT_V4 = new("c38d57d1-05a7-4c33-904f-7fbceee60e82");
}

/// <summary>
/// WFP condition field identifiers.
/// These GUIDs identify which field a filter condition matches against.
/// </summary>
internal static class WfpConditionGuids
{
    /// <summary>
    /// FWPM_CONDITION_IP_REMOTE_ADDRESS: The remote IP address.
    /// </summary>
    public static readonly Guid FWPM_CONDITION_IP_REMOTE_ADDRESS = new("b235ae9a-1d64-49b8-a44c-5ff3d9095045");

    /// <summary>
    /// FWPM_CONDITION_IP_REMOTE_PORT: The remote port number.
    /// </summary>
    public static readonly Guid FWPM_CONDITION_IP_REMOTE_PORT = new("c35a604d-d22b-4e1a-91b4-68f674ee674b");

    /// <summary>
    /// FWPM_CONDITION_IP_PROTOCOL: The IP protocol number (e.g., 6 for TCP, 17 for UDP).
    /// </summary>
    public static readonly Guid FWPM_CONDITION_IP_PROTOCOL = new("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7");

    /// <summary>
    /// FWPM_CONDITION_ALE_APP_ID: The application identifier (device path to executable).
    /// </summary>
    public static readonly Guid FWPM_CONDITION_ALE_APP_ID = new("d78e1e87-8644-4ea5-9437-d809ecefc971");
}

// ========================================
// Filter Structures
// ========================================

/// <summary>
/// Structure for FWPM_FILTER0 used in FwpmFilterAdd0.
/// Note: The rawContext field is a union in Windows SDK (UINT64 or GUID).
/// We use Guid (16 bytes) to ensure correct alignment.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_FILTER0
{
    /// <summary>Uniquely identifies the filter. If null GUID, a GUID will be generated.</summary>
    public Guid filterKey;
    /// <summary>Display name and description.</summary>
    public FWPM_DISPLAY_DATA0 displayData;
    /// <summary>Filter flags (see FwpmFilterFlags).</summary>
    public uint flags;
    /// <summary>Pointer to provider GUID that owns this filter (or null).</summary>
    public IntPtr providerKey;
    /// <summary>Opaque provider data (set to empty).</summary>
    public FWP_BYTE_BLOB providerData;
    /// <summary>GUID of the layer where this filter applies.</summary>
    public Guid layerKey;
    /// <summary>GUID of the sublayer where this filter is added.</summary>
    public Guid subLayerKey;
    /// <summary>Filter weight (higher = higher priority). Use FWP_VALUE0 with type=FWP_UINT64.</summary>
    public FWP_VALUE0 weight;
    /// <summary>Number of filter conditions.</summary>
    public uint numFilterConditions;
    /// <summary>Pointer to array of FWPM_FILTER_CONDITION0 structures.</summary>
    public IntPtr filterCondition;
    /// <summary>Action to take when filter matches.</summary>
    public FWPM_ACTION0 action;
    /// <summary>
    /// Union field: rawContext (UINT64) or providerContextKey (GUID).
    /// Using Guid (16 bytes) to match the union size.
    /// </summary>
    public Guid rawContextOrProviderContextKey;
    /// <summary>Reserved, must be null.</summary>
    public IntPtr reserved;
    /// <summary>Runtime filter ID (output only, set by FwpmFilterAdd0).</summary>
    public ulong filterId;
    /// <summary>Effective weight (output only).</summary>
    public FWP_VALUE0 effectiveWeight;
}

/// <summary>
/// Filter action structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_ACTION0
{
    /// <summary>Action type (block, permit, callout).</summary>
    public uint type;
    /// <summary>Callout GUID if action is callout, otherwise zero.</summary>
    public Guid filterType;
}

/// <summary>
/// Filter action types.
/// Action types must include the FWP_ACTION_FLAG_TERMINATING flag for block/permit.
/// </summary>
internal static class FwpActionType
{
    /// <summary>Flag indicating the action terminates filter evaluation.</summary>
    public const uint FWP_ACTION_FLAG_TERMINATING = 0x00001000;

    /// <summary>Block the traffic (terminating action).</summary>
    public const uint FWP_ACTION_BLOCK = 0x00000001 | FWP_ACTION_FLAG_TERMINATING; // 0x1001

    /// <summary>Permit the traffic (terminating action).</summary>
    public const uint FWP_ACTION_PERMIT = 0x00000002 | FWP_ACTION_FLAG_TERMINATING; // 0x1002
}

/// <summary>
/// Filter condition structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_FILTER_CONDITION0
{
    /// <summary>GUID of the condition field to match.</summary>
    public Guid fieldKey;
    /// <summary>Match type (equal, greater, range, etc.).</summary>
    public uint matchType;
    /// <summary>Value to compare against.</summary>
    public FWP_CONDITION_VALUE0 conditionValue;
}

/// <summary>
/// Match types for filter conditions.
/// </summary>
internal static class FwpMatchType
{
    /// <summary>Exact match.</summary>
    public const uint FWP_MATCH_EQUAL = 0;
    /// <summary>Greater than.</summary>
    public const uint FWP_MATCH_GREATER = 1;
    /// <summary>Less than.</summary>
    public const uint FWP_MATCH_LESS = 2;
    /// <summary>Greater than or equal.</summary>
    public const uint FWP_MATCH_GREATER_OR_EQUAL = 3;
    /// <summary>Less than or equal.</summary>
    public const uint FWP_MATCH_LESS_OR_EQUAL = 4;
    /// <summary>Range match (inclusive).</summary>
    public const uint FWP_MATCH_RANGE = 5;
}

/// <summary>
/// Byte blob structure for opaque data.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWP_BYTE_BLOB
{
    public uint size;
    public IntPtr data;
}

/// <summary>
/// Generic value structure used in filters.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWP_VALUE0
{
    /// <summary>Type of the value (see FwpDataType).</summary>
    public uint type;
    /// <summary>The value (interpretation depends on type).</summary>
    public ulong value;
}

/// <summary>
/// Condition value structure (same layout as FWP_VALUE0 but semantically different).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWP_CONDITION_VALUE0
{
    /// <summary>Type of the value (see FwpDataType).</summary>
    public uint type;
    /// <summary>The value (interpretation depends on type). For pointers, use IntPtr.</summary>
    public ulong value;
}

/// <summary>
/// Data types for FWP_VALUE0 and FWP_CONDITION_VALUE0.
/// Values from Windows SDK fwptypes.h
/// </summary>
internal static class FwpDataType
{
    /// <summary>Empty/unused value.</summary>
    public const uint FWP_EMPTY = 0;
    /// <summary>8-bit unsigned integer.</summary>
    public const uint FWP_UINT8 = 1;
    /// <summary>16-bit unsigned integer.</summary>
    public const uint FWP_UINT16 = 2;
    /// <summary>32-bit unsigned integer.</summary>
    public const uint FWP_UINT32 = 3;
    /// <summary>64-bit unsigned integer.</summary>
    public const uint FWP_UINT64 = 4;
    /// <summary>Byte blob (pointer to FWP_BYTE_BLOB).</summary>
    public const uint FWP_BYTE_BLOB_TYPE = 5;
    /// <summary>Range (pointer to FWP_RANGE0).</summary>
    public const uint FWP_RANGE_TYPE = 7;
    /// <summary>IPv4 address with mask (pointer to FWP_V4_ADDR_AND_MASK).</summary>
    public const uint FWP_V4_ADDR_MASK = 0x100;  // 256 in SDK fwptypes.h
}

/// <summary>
/// IPv4 address and mask for condition matching.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWP_V4_ADDR_AND_MASK
{
    /// <summary>IPv4 address in host byte order.</summary>
    public uint addr;
    /// <summary>Subnet mask in host byte order (0xFFFFFFFF for exact match).</summary>
    public uint mask;
}

/// <summary>
/// Range structure for FWP_MATCH_RANGE condition matching.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWP_RANGE0
{
    /// <summary>Low end of the range (inclusive).</summary>
    public FWP_VALUE0 valueLow;
    /// <summary>High end of the range (inclusive).</summary>
    public FWP_VALUE0 valueHigh;
}

/// <summary>
/// Filter flags for FWPM_FILTER0.
/// </summary>
internal static class FwpmFilterFlags
{
    /// <summary>No special flags.</summary>
    public const uint FWPM_FILTER_FLAG_NONE = 0x00000000;
}

// ========================================
// Filter Enumeration Structures
// ========================================

/// <summary>
/// Template for filter enumeration. Used to scope enumeration to specific filters.
/// All pointer fields are optional - if null, that field is not used for filtering.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_FILTER_ENUM_TEMPLATE0
{
    /// <summary>Pointer to provider GUID to filter by. Null to enumerate all providers.</summary>
    public IntPtr providerKey;
    /// <summary>GUID of layer to filter by. Guid.Empty to enumerate all layers.</summary>
    public Guid layerKey;
    /// <summary>Type of enumeration (FWP_FILTER_ENUM_TYPE).</summary>
    public uint enumType;
    /// <summary>Filter flags to match.</summary>
    public uint flags;
    /// <summary>Pointer to provider context GUID. Null to enumerate all.</summary>
    public IntPtr providerContextTemplate;
    /// <summary>Number of filter conditions.</summary>
    public uint numFilterConditions;
    /// <summary>Pointer to array of filter conditions. Null for no condition filtering.</summary>
    public IntPtr filterCondition;
    /// <summary>Action type to match. 0xFFFFFFFF (FWP_ACTION_TYPE_ALL) to enumerate all.</summary>
    public uint actionMask;
    /// <summary>Pointer to callout GUID to filter by. Null to enumerate all.</summary>
    public IntPtr calloutKey;
}

/// <summary>
/// Filter enumeration types.
/// </summary>
internal static class FwpFilterEnumType
{
    /// <summary>Enumerate all filters matching the template.</summary>
    public const uint FWP_FILTER_ENUM_FULLY_CONTAINED = 0;
    /// <summary>Enumerate filters that overlap with template conditions.</summary>
    public const uint FWP_FILTER_ENUM_OVERLAPPING = 1;
}

/// <summary>
/// Action type mask for enumeration.
/// </summary>
internal static class FwpActionTypeMask
{
    /// <summary>Match all action types.</summary>
    public const uint FWP_ACTION_TYPE_ALL = 0xFFFFFFFF;
}
