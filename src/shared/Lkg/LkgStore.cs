// src/shared/Lkg/LkgStore.cs
// LKG (Last Known Good) policy persistence
// Phase 14: LKG Persistence and Fail-Open Behavior

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.Lkg;

/// <summary>
/// Wrapper for storing policy with integrity checksum.
/// </summary>
public sealed class LkgPolicyWrapper
{
    /// <summary>
    /// SHA256 hash of the policy JSON for integrity verification.
    /// </summary>
    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// The policy JSON string.
    /// </summary>
    [JsonPropertyName("policyJson")]
    public string PolicyJson { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the LKG was saved.
    /// </summary>
    [JsonPropertyName("savedAt")]
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// Path to the original policy file that was applied.
    /// </summary>
    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; set; }
}

/// <summary>
/// Result of loading an LKG policy.
/// </summary>
public sealed class LkgLoadResult
{
    /// <summary>
    /// Whether an LKG policy exists and was loaded successfully.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// The loaded policy, if successful.
    /// </summary>
    public Policy.Policy? Policy { get; init; }

    /// <summary>
    /// The raw policy JSON, if successful.
    /// </summary>
    public string? PolicyJson { get; init; }

    /// <summary>
    /// When the LKG was saved.
    /// </summary>
    public DateTime? SavedAt { get; init; }

    /// <summary>
    /// Source path from which the original policy was applied.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static LkgLoadResult Success(Policy.Policy policy, string policyJson, DateTime savedAt, string? sourcePath)
    {
        return new LkgLoadResult
        {
            Exists = true,
            Policy = policy,
            PolicyJson = policyJson,
            SavedAt = savedAt,
            SourcePath = sourcePath
        };
    }

    /// <summary>
    /// Creates a "not found" result.
    /// </summary>
    public static LkgLoadResult NotFound()
    {
        return new LkgLoadResult
        {
            Exists = false
        };
    }

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static LkgLoadResult Failed(string error)
    {
        return new LkgLoadResult
        {
            Exists = false,
            Error = error
        };
    }
}

/// <summary>
/// Handles persistence of LKG (Last Known Good) policy to disk.
/// Uses atomic writes and checksum verification for safety.
/// </summary>
public static class LkgStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Saves a policy as the LKG (Last Known Good) policy.
    /// Uses atomic write (temp file + rename) for safety.
    /// </summary>
    /// <param name="policyJson">The policy JSON to save.</param>
    /// <param name="sourcePath">Optional path to the source policy file.</param>
    /// <returns>Result indicating success or failure.</returns>
    public static Result Save(string policyJson, string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(policyJson))
        {
            return Result.Failure(ErrorCodes.InvalidArgument, "Policy JSON cannot be empty");
        }

        try
        {
            // Ensure directory exists
            var directory = WfpConstants.GetDataDirectory();
            Directory.CreateDirectory(directory);

            // Compute checksum
            var checksum = ComputeChecksum(policyJson);

            // Create wrapper
            var wrapper = new LkgPolicyWrapper
            {
                Checksum = checksum,
                PolicyJson = policyJson,
                SavedAt = DateTime.UtcNow,
                SourcePath = sourcePath
            };

            // Serialize wrapper
            var wrapperJson = JsonSerializer.Serialize(wrapper, JsonOptions);

            // Atomic write: write to temp file, then rename
            var lkgPath = WfpConstants.GetLkgPolicyPath();
            var tempPath = lkgPath + ".tmp";

            File.WriteAllText(tempPath, wrapperJson, Encoding.UTF8);

            // Replace existing file atomically
            File.Move(tempPath, lkgPath, overwrite: true);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ErrorCodes.ServiceError, $"Failed to save LKG policy: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the LKG policy from disk with integrity verification.
    /// Returns NotFound if no LKG exists, Failed if corrupt/invalid.
    /// </summary>
    /// <returns>Load result with policy or error information.</returns>
    public static LkgLoadResult Load()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();

        // Check if file exists
        if (!File.Exists(lkgPath))
        {
            return LkgLoadResult.NotFound();
        }

        try
        {
            // Read wrapper file
            var wrapperJson = File.ReadAllText(lkgPath, Encoding.UTF8);

            // Parse wrapper
            var wrapper = JsonSerializer.Deserialize<LkgPolicyWrapper>(wrapperJson, JsonOptions);
            if (wrapper == null)
            {
                return LkgLoadResult.Failed("LKG file is empty or invalid");
            }

            // Verify checksum
            if (string.IsNullOrEmpty(wrapper.Checksum) || string.IsNullOrEmpty(wrapper.PolicyJson))
            {
                return LkgLoadResult.Failed("LKG file is missing checksum or policy");
            }

            var expectedChecksum = ComputeChecksum(wrapper.PolicyJson);
            if (!string.Equals(wrapper.Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                return LkgLoadResult.Failed("LKG checksum mismatch - file may be corrupted");
            }

            // Validate the policy JSON
            var validationResult = PolicyValidator.ValidateJson(wrapper.PolicyJson);
            if (!validationResult.IsValid)
            {
                return LkgLoadResult.Failed($"LKG policy validation failed: {validationResult.GetSummary()}");
            }

            // Parse the policy
            var policy = Policy.Policy.FromJson(wrapper.PolicyJson);
            if (policy == null)
            {
                return LkgLoadResult.Failed("Failed to parse LKG policy");
            }

            return LkgLoadResult.Success(policy, wrapper.PolicyJson, wrapper.SavedAt, wrapper.SourcePath);
        }
        catch (JsonException ex)
        {
            return LkgLoadResult.Failed($"LKG file contains invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return LkgLoadResult.Failed($"Failed to load LKG policy: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if an LKG policy file exists.
    /// </summary>
    public static bool Exists()
    {
        return File.Exists(WfpConstants.GetLkgPolicyPath());
    }

    /// <summary>
    /// Deletes the LKG policy file if it exists.
    /// </summary>
    /// <returns>True if deleted, false if it didn't exist.</returns>
    public static Result<bool> Delete()
    {
        try
        {
            var lkgPath = WfpConstants.GetLkgPolicyPath();
            if (File.Exists(lkgPath))
            {
                File.Delete(lkgPath);
                return Result<bool>.Success(true);
            }
            return Result<bool>.Success(false);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ErrorCodes.ServiceError, $"Failed to delete LKG policy: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets metadata about the LKG policy without fully loading it.
    /// </summary>
    public static Result<LkgMetadata> GetMetadata()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();

        if (!File.Exists(lkgPath))
        {
            return Result<LkgMetadata>.Success(new LkgMetadata { Exists = false });
        }

        try
        {
            var wrapperJson = File.ReadAllText(lkgPath, Encoding.UTF8);
            var wrapper = JsonSerializer.Deserialize<LkgPolicyWrapper>(wrapperJson, JsonOptions);

            if (wrapper == null)
            {
                return Result<LkgMetadata>.Success(new LkgMetadata
                {
                    Exists = true,
                    IsCorrupt = true,
                    Error = "LKG file is empty or invalid"
                });
            }

            // Verify checksum
            var expectedChecksum = ComputeChecksum(wrapper.PolicyJson);
            var checksumValid = string.Equals(wrapper.Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

            if (!checksumValid)
            {
                return Result<LkgMetadata>.Success(new LkgMetadata
                {
                    Exists = true,
                    IsCorrupt = true,
                    Error = "LKG checksum mismatch"
                });
            }

            // Try to parse policy for metadata
            var policy = Policy.Policy.FromJson(wrapper.PolicyJson);

            return Result<LkgMetadata>.Success(new LkgMetadata
            {
                Exists = true,
                IsCorrupt = false,
                SavedAt = wrapper.SavedAt,
                SourcePath = wrapper.SourcePath,
                PolicyVersion = policy?.Version,
                RuleCount = policy?.Rules.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            return Result<LkgMetadata>.Success(new LkgMetadata
            {
                Exists = true,
                IsCorrupt = true,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Computes SHA256 checksum of a string.
    /// </summary>
    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Metadata about an LKG policy file.
/// </summary>
public sealed class LkgMetadata
{
    public bool Exists { get; init; }
    public bool IsCorrupt { get; init; }
    public string? Error { get; init; }
    public DateTime? SavedAt { get; init; }
    public string? SourcePath { get; init; }
    public string? PolicyVersion { get; init; }
    public int RuleCount { get; init; }
}
