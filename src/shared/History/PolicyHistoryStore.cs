using System.Text.Json;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.History;

/// <summary>
/// Entry in the policy history representing a single policy version.
/// </summary>
public sealed class PolicyHistoryEntry
{
    /// <summary>
    /// Unique identifier for this history entry.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this policy was applied.
    /// </summary>
    public DateTime AppliedAt { get; set; }

    /// <summary>
    /// Policy version from the policy file.
    /// </summary>
    public string PolicyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Number of rules in the policy.
    /// </summary>
    public int RuleCount { get; set; }

    /// <summary>
    /// Source of the apply operation (CLI, UI, Watch, LKG).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Original source file path (if applicable).
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Number of filters created by this policy.
    /// </summary>
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of filters removed when applying this policy.
    /// </summary>
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// File name where the policy JSON is stored.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Stores and retrieves policy history for versioning and rollback.
/// </summary>
public static class PolicyHistoryStore
{
    private const string HistoryFolderName = "History";
    private const string IndexFileName = "history-index.json";
    private const int DefaultMaxEntries = 100;

    private static readonly object _lock = new();

    /// <summary>
    /// Gets the history storage directory path.
    /// </summary>
    public static string GetHistoryPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(basePath, "WfpTrafficControl", HistoryFolderName);
    }

    /// <summary>
    /// Saves a policy to history after successful apply.
    /// </summary>
    public static Result<PolicyHistoryEntry> Save(
        string policyJson,
        string? sourcePath,
        string source,
        int filtersCreated,
        int filtersRemoved)
    {
        lock (_lock)
        {
            try
            {
                // Parse the policy to get metadata
                var policy = Policy.Policy.FromJson(policyJson);
                if (policy == null)
                {
                    return Result<PolicyHistoryEntry>.Failure(new Error("PARSE_ERROR", "Failed to parse policy JSON"));
                }

                // Ensure directory exists
                var historyPath = GetHistoryPath();
                Directory.CreateDirectory(historyPath);

                // Generate entry ID based on timestamp
                var appliedAt = DateTime.UtcNow;
                var entryId = appliedAt.ToString("yyyyMMdd-HHmmss-fff");
                var policyFileName = $"policy-{entryId}.json";

                // Create history entry
                var entry = new PolicyHistoryEntry
                {
                    Id = entryId,
                    AppliedAt = appliedAt,
                    PolicyVersion = policy.Version ?? "unknown",
                    RuleCount = policy.Rules?.Count ?? 0,
                    Source = source,
                    SourcePath = sourcePath,
                    FiltersCreated = filtersCreated,
                    FiltersRemoved = filtersRemoved,
                    FileName = policyFileName
                };

                // Save the policy JSON
                var policyPath = Path.Combine(historyPath, policyFileName);
                File.WriteAllText(policyPath, policyJson);

                // Update the index
                var index = LoadIndex(historyPath);
                index.Insert(0, entry); // Most recent first

                // Trim old entries if needed
                TrimHistory(historyPath, index, DefaultMaxEntries);

                // Save the index
                SaveIndex(historyPath, index);

                return Result<PolicyHistoryEntry>.Success(entry);
            }
            catch (Exception ex)
            {
                return Result<PolicyHistoryEntry>.Failure(new Error("SAVE_ERROR", $"Failed to save policy history: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Gets the list of history entries.
    /// </summary>
    public static Result<List<PolicyHistoryEntry>> GetHistory(int limit = 50)
    {
        lock (_lock)
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (!Directory.Exists(historyPath))
                {
                    return Result<List<PolicyHistoryEntry>>.Success(new List<PolicyHistoryEntry>());
                }

                var index = LoadIndex(historyPath);
                var result = index.Take(limit).ToList();
                return Result<List<PolicyHistoryEntry>>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<List<PolicyHistoryEntry>>.Failure(new Error("LOAD_ERROR", $"Failed to load policy history: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Gets a specific history entry by ID.
    /// </summary>
    public static Result<PolicyHistoryEntry> GetEntry(string entryId)
    {
        lock (_lock)
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (!Directory.Exists(historyPath))
                {
                    return Result<PolicyHistoryEntry>.Failure(new Error("NOT_FOUND", "No history found"));
                }

                var index = LoadIndex(historyPath);
                var entry = index.FirstOrDefault(e => e.Id == entryId);

                if (entry == null)
                {
                    return Result<PolicyHistoryEntry>.Failure(new Error("NOT_FOUND", $"History entry not found: {entryId}"));
                }

                return Result<PolicyHistoryEntry>.Success(entry);
            }
            catch (Exception ex)
            {
                return Result<PolicyHistoryEntry>.Failure(new Error("LOAD_ERROR", $"Failed to get history entry: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Loads the policy JSON for a specific history entry.
    /// </summary>
    public static Result<string> LoadPolicyJson(string entryId)
    {
        lock (_lock)
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (!Directory.Exists(historyPath))
                {
                    return Result<string>.Failure(new Error("NOT_FOUND", "No history found"));
                }

                var index = LoadIndex(historyPath);
                var entry = index.FirstOrDefault(e => e.Id == entryId);

                if (entry == null)
                {
                    return Result<string>.Failure(new Error("NOT_FOUND", $"History entry not found: {entryId}"));
                }

                var policyPath = Path.Combine(historyPath, entry.FileName);
                if (!File.Exists(policyPath))
                {
                    return Result<string>.Failure(new Error("FILE_MISSING", $"Policy file missing: {entry.FileName}"));
                }

                var json = File.ReadAllText(policyPath);
                return Result<string>.Success(json);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new Error("LOAD_ERROR", $"Failed to load policy: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Loads the policy object for a specific history entry.
    /// </summary>
    public static Result<Policy.Policy> LoadPolicy(string entryId)
    {
        var jsonResult = LoadPolicyJson(entryId);
        if (jsonResult.IsFailure)
        {
            return Result<Policy.Policy>.Failure(jsonResult.Error);
        }

        var policy = Policy.Policy.FromJson(jsonResult.Value);
        if (policy == null)
        {
            return Result<Policy.Policy>.Failure(new Error("PARSE_ERROR", "Failed to parse policy JSON"));
        }

        return Result<Policy.Policy>.Success(policy);
    }

    /// <summary>
    /// Gets the count of history entries.
    /// </summary>
    public static int GetCount()
    {
        lock (_lock)
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (!Directory.Exists(historyPath))
                {
                    return 0;
                }

                var index = LoadIndex(historyPath);
                return index.Count;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Clears all history entries.
    /// </summary>
    public static Result<int> Clear()
    {
        lock (_lock)
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (!Directory.Exists(historyPath))
                {
                    return Result<int>.Success(0);
                }

                var index = LoadIndex(historyPath);
                var count = index.Count;

                // Delete all policy files
                foreach (var entry in index)
                {
                    var policyPath = Path.Combine(historyPath, entry.FileName);
                    if (File.Exists(policyPath))
                    {
                        File.Delete(policyPath);
                    }
                }

                // Clear the index
                index.Clear();
                SaveIndex(historyPath, index);

                return Result<int>.Success(count);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure(new Error("CLEAR_ERROR", $"Failed to clear history: {ex.Message}"));
            }
        }
    }

    private static List<PolicyHistoryEntry> LoadIndex(string historyPath)
    {
        var indexPath = Path.Combine(historyPath, IndexFileName);
        if (!File.Exists(indexPath))
        {
            return new List<PolicyHistoryEntry>();
        }

        var json = File.ReadAllText(indexPath);
        return JsonSerializer.Deserialize<List<PolicyHistoryEntry>>(json) ?? new List<PolicyHistoryEntry>();
    }

    private static void SaveIndex(string historyPath, List<PolicyHistoryEntry> index)
    {
        var indexPath = Path.Combine(historyPath, IndexFileName);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(index, options);
        File.WriteAllText(indexPath, json);
    }

    private static void TrimHistory(string historyPath, List<PolicyHistoryEntry> index, int maxEntries)
    {
        while (index.Count > maxEntries)
        {
            var oldest = index[^1];

            // Delete the policy file
            var policyPath = Path.Combine(historyPath, oldest.FileName);
            if (File.Exists(policyPath))
            {
                try
                {
                    File.Delete(policyPath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            index.RemoveAt(index.Count - 1);
        }
    }
}
