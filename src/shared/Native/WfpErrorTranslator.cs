using System.ComponentModel;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// Translates Win32 error codes from WFP API calls into Error objects.
/// </summary>
public static class WfpErrorTranslator
{
    // Common Win32 error codes
    private const uint ERROR_SUCCESS = 0;
    private const uint ERROR_ACCESS_DENIED = 5;
    private const uint ERROR_INVALID_PARAMETER = 87;
    private const uint ERROR_NOT_FOUND = 1168;

    // WFP error codes - primary values (from Windows SDK)
    private const uint FWP_E_NOT_FOUND = 0x80320002;
    private const uint FWP_E_FILTER_NOT_FOUND = 0x80320003;
    private const uint FWP_E_PROVIDER_NOT_FOUND = 0x80320005;
    private const uint FWP_E_IN_USE = 0x80320006;
    private const uint FWP_E_SUBLAYER_NOT_FOUND = 0x80320007;
    private const uint FWP_E_ALREADY_EXISTS = 0x80320009;
    private const uint FWP_E_SESSION_ABORTED = 0x80320017;
    private const uint FWP_E_INVALID_PARAMETER = 0x80320035;

    // WFP error codes - alternative values (observed on some Windows versions)
    private const uint FWP_E_PROVIDER_NOT_FOUND_ALT = 0x80320008;
    private const uint FWP_E_SUBLAYER_NOT_FOUND_ALT = 0x8032000A;
    private const uint FWP_E_FILTER_NOT_FOUND_ALT = 0x8032000B;

    /// <summary>
    /// Checks if the given error code represents success.
    /// </summary>
    public static bool IsSuccess(uint errorCode) => errorCode == ERROR_SUCCESS;

    /// <summary>
    /// Translates a Win32/WFP error code into an Error object.
    /// Returns null if the error code indicates success.
    /// </summary>
    /// <param name="errorCode">The Win32 or WFP error code.</param>
    /// <param name="context">Optional context to include in the error message (e.g., "opening engine").</param>
    /// <returns>An Error object, or null if the operation succeeded.</returns>
    public static Error? TranslateError(uint errorCode, string? context = null)
    {
        if (errorCode == ERROR_SUCCESS)
            return null;

        var (code, baseMessage) = GetErrorDetails(errorCode);
        var message = string.IsNullOrEmpty(context)
            ? baseMessage
            : $"{context}: {baseMessage}";

        return new Error(code, message);
    }

    /// <summary>
    /// Creates a Result indicating failure from the given Win32/WFP error code.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="errorCode">The Win32 or WFP error code.</param>
    /// <param name="context">Optional context to include in the error message.</param>
    /// <returns>A failed Result with the translated error.</returns>
    public static Result<T> ToFailedResult<T>(uint errorCode, string? context = null)
    {
        var error = TranslateError(errorCode, context);
        if (error == null)
            throw new ArgumentException("Cannot create failed result from success error code.", nameof(errorCode));

        return Result<T>.Failure(error);
    }

    /// <summary>
    /// Creates a non-generic Result indicating failure from the given Win32/WFP error code.
    /// </summary>
    /// <param name="errorCode">The Win32 or WFP error code.</param>
    /// <param name="context">Optional context to include in the error message.</param>
    /// <returns>A failed Result with the translated error.</returns>
    public static Result ToFailedResult(uint errorCode, string? context = null)
    {
        var error = TranslateError(errorCode, context);
        if (error == null)
            throw new ArgumentException("Cannot create failed result from success error code.", nameof(errorCode));

        return Result.Failure(error);
    }

    /// <summary>
    /// Gets the error code and base message for a given Win32/WFP error code.
    /// </summary>
    private static (string Code, string Message) GetErrorDetails(uint errorCode)
    {
        return errorCode switch
        {
            ERROR_ACCESS_DENIED => (ErrorCodes.AccessDenied, "Access denied. Ensure the process is running with administrator privileges."),
            ERROR_INVALID_PARAMETER => (ErrorCodes.InvalidArgument, "Invalid parameter passed to WFP API."),
            ERROR_NOT_FOUND => (ErrorCodes.NotFound, "The specified object was not found."),
            FWP_E_ALREADY_EXISTS => (ErrorCodes.WfpError, "The WFP object already exists."),
            FWP_E_IN_USE => (ErrorCodes.WfpError, "The WFP object is in use and cannot be modified."),
            FWP_E_NOT_FOUND => (ErrorCodes.NotFound, "The WFP object was not found."),
            FWP_E_SESSION_ABORTED => (ErrorCodes.WfpError, "The WFP session was aborted."),
            FWP_E_INVALID_PARAMETER => (ErrorCodes.InvalidArgument, "Invalid parameter in WFP call."),
            // Provider not found - both primary (0x80320005) and alternative (0x80320008) codes
            FWP_E_PROVIDER_NOT_FOUND or FWP_E_PROVIDER_NOT_FOUND_ALT =>
                (ErrorCodes.NotFound, "The WFP provider was not found."),
            // Sublayer not found - both primary (0x80320007) and alternative (0x8032000A) codes
            FWP_E_SUBLAYER_NOT_FOUND or FWP_E_SUBLAYER_NOT_FOUND_ALT =>
                (ErrorCodes.NotFound, "The WFP sublayer was not found."),
            // Filter not found - both primary (0x80320003) and alternative (0x8032000B) codes
            FWP_E_FILTER_NOT_FOUND or FWP_E_FILTER_NOT_FOUND_ALT =>
                (ErrorCodes.NotFound, "The WFP filter was not found."),
            _ => (ErrorCodes.WfpError, GetWin32ErrorMessage(errorCode))
        };
    }

    /// <summary>
    /// Gets the Win32 error message for an error code.
    /// </summary>
    private static string GetWin32ErrorMessage(uint errorCode)
    {
        try
        {
            var message = new Win32Exception((int)errorCode).Message;
            return $"WFP operation failed with error 0x{errorCode:X8}: {message}";
        }
        catch
        {
            return $"WFP operation failed with error code 0x{errorCode:X8}.";
        }
    }
}
