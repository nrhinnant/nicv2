// src/shared/Policy/NetworkUtils.cs
// Network utility functions for CIDR and port validation
// Phase 11: Policy Schema v1

using System.Net;
using System.Text.RegularExpressions;

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Utility class for parsing and validating network-related fields.
/// </summary>
public static partial class NetworkUtils
{
    /// <summary>
    /// Minimum valid port number.
    /// </summary>
    public const int MinPort = 1;

    /// <summary>
    /// Maximum valid port number.
    /// </summary>
    public const int MaxPort = 65535;

    /// <summary>
    /// Validates an IP address or CIDR notation string.
    /// </summary>
    /// <param name="ipOrCidr">IP address or CIDR (e.g., "192.168.1.1", "10.0.0.0/8", "::1/128")</param>
    /// <param name="error">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateIpOrCidr(string? ipOrCidr, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(ipOrCidr))
        {
            error = "IP/CIDR cannot be empty";
            return false;
        }

        var trimmed = ipOrCidr.Trim();

        // Check for CIDR notation
        if (trimmed.Contains('/'))
        {
            return ValidateCidr(trimmed, out error);
        }

        // Plain IP address
        if (!IPAddress.TryParse(trimmed, out var ip))
        {
            error = $"Invalid IP address: '{trimmed}'";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a CIDR notation string (e.g., "192.168.1.0/24").
    /// </summary>
    private static bool ValidateCidr(string cidr, out string? error)
    {
        error = null;

        var parts = cidr.Split('/', 2);
        if (parts.Length != 2)
        {
            error = $"Invalid CIDR format: '{cidr}'";
            return false;
        }

        var ipPart = parts[0];
        var prefixPart = parts[1];

        // Validate IP portion
        if (!IPAddress.TryParse(ipPart, out var ip))
        {
            error = $"Invalid IP address in CIDR: '{ipPart}'";
            return false;
        }

        // Validate prefix length
        if (!int.TryParse(prefixPart, out var prefix))
        {
            error = $"Invalid prefix length in CIDR: '{prefixPart}'";
            return false;
        }

        // Check prefix range based on address family
        var maxPrefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefix < 0 || prefix > maxPrefix)
        {
            error = $"Prefix length {prefix} out of range for {(maxPrefix == 32 ? "IPv4" : "IPv6")} (0-{maxPrefix})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a CIDR string into IP address and prefix length.
    /// </summary>
    /// <param name="cidr">CIDR notation string</param>
    /// <param name="ip">Parsed IP address</param>
    /// <param name="prefixLength">Parsed prefix length (32 for plain IPv4, 128 for plain IPv6)</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParseCidr(string cidr, out IPAddress? ip, out int prefixLength)
    {
        ip = null;
        prefixLength = 0;

        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var trimmed = cidr.Trim();

        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/', 2);
            if (!IPAddress.TryParse(parts[0], out ip))
                return false;

            if (!int.TryParse(parts[1], out prefixLength))
                return false;

            var maxPrefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength < 0 || prefixLength > maxPrefix)
                return false;
        }
        else
        {
            if (!IPAddress.TryParse(trimmed, out ip))
                return false;

            prefixLength = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        }

        return true;
    }

    /// <summary>
    /// Validates a port specification string.
    /// Supports: single port ("80"), range ("80-443"), comma-separated ("80,443,8080-8090")
    /// </summary>
    /// <param name="ports">Port specification string</param>
    /// <param name="error">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidatePorts(string? ports, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(ports))
        {
            error = "Ports specification cannot be empty";
            return false;
        }

        var trimmed = ports.Trim();

        // Split by comma to handle lists
        var segments = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            error = "Ports specification cannot be empty";
            return false;
        }

        foreach (var segment in segments)
        {
            var seg = segment.Trim();
            if (string.IsNullOrEmpty(seg))
            {
                error = "Empty port segment in list";
                return false;
            }

            // Check for range (contains dash but not starting with dash for negative)
            if (seg.Contains('-') && !seg.StartsWith('-'))
            {
                if (!ValidatePortRange(seg, out error))
                    return false;
            }
            else
            {
                if (!ValidateSinglePort(seg, out error))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a single port number.
    /// </summary>
    private static bool ValidateSinglePort(string port, out string? error)
    {
        error = null;

        if (!int.TryParse(port, out var portNum))
        {
            error = $"Invalid port number: '{port}'";
            return false;
        }

        if (portNum < MinPort || portNum > MaxPort)
        {
            error = $"Port {portNum} out of range ({MinPort}-{MaxPort})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a port range (e.g., "80-443").
    /// </summary>
    private static bool ValidatePortRange(string range, out string? error)
    {
        error = null;

        var parts = range.Split('-', 2);
        if (parts.Length != 2)
        {
            error = $"Invalid port range format: '{range}'";
            return false;
        }

        if (!int.TryParse(parts[0].Trim(), out var startPort))
        {
            error = $"Invalid start port in range: '{parts[0]}'";
            return false;
        }

        if (!int.TryParse(parts[1].Trim(), out var endPort))
        {
            error = $"Invalid end port in range: '{parts[1]}'";
            return false;
        }

        if (startPort < MinPort || startPort > MaxPort)
        {
            error = $"Start port {startPort} out of range ({MinPort}-{MaxPort})";
            return false;
        }

        if (endPort < MinPort || endPort > MaxPort)
        {
            error = $"End port {endPort} out of range ({MinPort}-{MaxPort})";
            return false;
        }

        if (startPort > endPort)
        {
            error = $"Port range start ({startPort}) cannot be greater than end ({endPort})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a port specification into a list of port ranges.
    /// Each range is a tuple of (start, end) where start == end for single ports.
    /// </summary>
    /// <param name="ports">Port specification string</param>
    /// <param name="ranges">List of (start, end) port ranges</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParsePorts(string ports, out List<(int Start, int End)> ranges)
    {
        ranges = new List<(int, int)>();

        if (string.IsNullOrWhiteSpace(ports))
            return false;

        var segments = ports.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var seg = segment.Trim();
            if (string.IsNullOrEmpty(seg))
                return false;

            if (seg.Contains('-') && !seg.StartsWith('-'))
            {
                var parts = seg.Split('-', 2);
                if (parts.Length != 2)
                    return false;

                if (!int.TryParse(parts[0].Trim(), out var start) ||
                    !int.TryParse(parts[1].Trim(), out var end))
                    return false;

                if (start < MinPort || start > MaxPort || end < MinPort || end > MaxPort || start > end)
                    return false;

                ranges.Add((start, end));
            }
            else
            {
                if (!int.TryParse(seg, out var port))
                    return false;

                if (port < MinPort || port > MaxPort)
                    return false;

                ranges.Add((port, port));
            }
        }

        return ranges.Count > 0;
    }

    /// <summary>
    /// Validates a Windows file path format.
    /// Does not check if the file exists, only validates format.
    /// </summary>
    /// <param name="path">File path to validate</param>
    /// <param name="error">Error message if validation fails</param>
    /// <returns>True if valid format, false otherwise</returns>
    public static bool ValidateProcessPath(string? path, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Process path cannot be empty";
            return false;
        }

        var trimmed = path.Trim();

        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (trimmed.IndexOfAny(invalidChars) >= 0)
        {
            error = $"Process path contains invalid characters";
            return false;
        }

        // Check for path traversal attempts
        if (trimmed.Contains(".."))
        {
            error = "Process path cannot contain '..' (path traversal)";
            return false;
        }

        // Must be either a full path or just an image name
        // Full path: starts with drive letter or UNC
        // Image name: no path separators
        var hasPathSeparator = trimmed.Contains('\\') || trimmed.Contains('/');

        if (hasPathSeparator)
        {
            // Should be a full path
            // Check for drive letter pattern (C:\) or UNC (\\server\)
            if (!FullPathRegex().IsMatch(trimmed))
            {
                error = "Process path must be a full path (e.g., C:\\Program Files\\app.exe) or just an image name (e.g., app.exe)";
                return false;
            }
        }
        else
        {
            // Should be an image name (just filename)
            // Check it looks like a filename
            if (!ImageNameRegex().IsMatch(trimmed))
            {
                error = "Process image name must be a valid filename (e.g., app.exe)";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a semantic version string (e.g., "1.0.0", "2.1.3-beta").
    /// </summary>
    /// <param name="version">Version string to validate</param>
    /// <param name="error">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateVersion(string? version, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(version))
        {
            error = "Version cannot be empty";
            return false;
        }

        // Semantic versioning pattern: MAJOR.MINOR.PATCH with optional pre-release
        if (!SemVerRegex().IsMatch(version.Trim()))
        {
            error = $"Invalid version format: '{version}'. Expected semantic version (e.g., 1.0.0)";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^[A-Za-z]:\\|^\\\\", RegexOptions.Compiled)]
    private static partial Regex FullPathRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]+$", RegexOptions.Compiled)]
    private static partial Regex ImageNameRegex();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[A-Za-z0-9\.\-]+)?(\+[A-Za-z0-9\.\-]+)?$", RegexOptions.Compiled)]
    private static partial Regex SemVerRegex();
}
