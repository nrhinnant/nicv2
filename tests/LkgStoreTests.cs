using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Lkg;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for LKG (Last Known Good) policy persistence.
/// Phase 14: LKG Persistence and Fail-Open Behavior
/// </summary>
public class LkgStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _originalDataDirectory;

    public LkgStoreTests()
    {
        // Create a unique test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LkgStoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Store original path to restore later
        _originalDataDirectory = WfpConstants.GetDataDirectory();
    }

    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    #region Helper Methods

    private static string CreateValidPolicyJson(string version = "1.0.0", int ruleCount = 1)
    {
        var rules = new List<string>();
        for (int i = 0; i < ruleCount; i++)
        {
            rules.Add($$"""
                {
                    "id": "test-rule-{{i}}",
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "remote": { "ip": "1.1.1.{{i + 1}}", "ports": "443" },
                    "priority": {{100 + i}},
                    "enabled": true
                }
                """);
        }

        return $$"""
            {
                "version": "{{version}}",
                "defaultAction": "allow",
                "updatedAt": "2024-01-15T10:30:00Z",
                "rules": [{{string.Join(",", rules)}}]
            }
            """;
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void WriteLkgFile(string checksum, string policyJson, DateTime? savedAt = null, string? sourcePath = null)
    {
        var wrapper = new
        {
            checksum,
            policyJson,
            savedAt = savedAt ?? DateTime.UtcNow,
            sourcePath
        };

        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, JsonSerializer.Serialize(wrapper));
    }

    #endregion

    #region Save Tests

    [Fact]
    public void Save_ValidPolicy_CreatesLkgFile()
    {
        var policyJson = CreateValidPolicyJson();

        var result = LkgStore.Save(policyJson, "C:\\test\\policy.json");

        Assert.True(result.IsSuccess);
        Assert.True(LkgStore.Exists());
    }

    [Fact]
    public void Save_ValidPolicy_IncludesChecksum()
    {
        var policyJson = CreateValidPolicyJson();

        var result = LkgStore.Save(policyJson);

        Assert.True(result.IsSuccess);

        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.NotNull(loadResult.PolicyJson);
    }

    [Fact]
    public void Save_ValidPolicy_PreservesSourcePath()
    {
        var policyJson = CreateValidPolicyJson();
        var sourcePath = "C:\\policies\\my-policy.json";

        LkgStore.Save(policyJson, sourcePath);

        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.Equal(sourcePath, loadResult.SourcePath);
    }

    [Fact]
    public void Save_EmptyPolicy_ReturnsFailure()
    {
        var result = LkgStore.Save("");

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_WhitespacePolicy_ReturnsFailure()
    {
        var result = LkgStore.Save("   \t\n  ");

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_OverwritesExistingLkg()
    {
        var policy1 = CreateValidPolicyJson(version: "1.0.0");
        var policy2 = CreateValidPolicyJson(version: "2.0.0");

        LkgStore.Save(policy1);
        LkgStore.Save(policy2);

        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.Equal("2.0.0", loadResult.Policy?.Version);
    }

    #endregion

    #region Load Tests

    [Fact]
    public void Load_ValidLkg_ReturnsPolicy()
    {
        var policyJson = CreateValidPolicyJson(version: "1.2.3", ruleCount: 2);
        LkgStore.Save(policyJson);

        var result = LkgStore.Load();

        Assert.True(result.Exists);
        Assert.NotNull(result.Policy);
        Assert.Equal("1.2.3", result.Policy!.Version);
        Assert.Equal(2, result.Policy.Rules.Count);
    }

    [Fact]
    public void Load_ValidLkg_ReturnsPolicyJson()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        var result = LkgStore.Load();

        Assert.True(result.Exists);
        Assert.NotNull(result.PolicyJson);
        Assert.Contains("version", result.PolicyJson);
    }

    [Fact]
    public void Load_ValidLkg_ReturnsSavedAtTimestamp()
    {
        var policyJson = CreateValidPolicyJson();
        var before = DateTime.UtcNow;
        LkgStore.Save(policyJson);
        var after = DateTime.UtcNow;

        var result = LkgStore.Load();

        Assert.True(result.Exists);
        Assert.NotNull(result.SavedAt);
        Assert.True(result.SavedAt >= before);
        Assert.True(result.SavedAt <= after);
    }

    [Fact]
    public void Load_MissingLkg_ReturnsNotFound()
    {
        // Ensure no LKG exists
        LkgStore.Delete();

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.Null(result.Policy);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Load_CorruptChecksum_ReturnsFailedWithError()
    {
        var policyJson = CreateValidPolicyJson();
        WriteLkgFile("invalid_checksum_value", policyJson);

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
        Assert.Contains("checksum", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsFailedWithError()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, "not valid json at all");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsFailedWithError()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, "");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Load_MissingPolicyJson_ReturnsFailedWithError()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, """{ "checksum": "abc", "savedAt": "2024-01-01T00:00:00Z" }""");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Load_InvalidPolicyJson_ReturnsFailedWithError()
    {
        // Policy with invalid version format
        var invalidPolicy = """{ "version": "invalid", "defaultAction": "allow", "updatedAt": "2024-01-15T10:30:00Z", "rules": [] }""";
        WriteLkgFile(ComputeChecksum(invalidPolicy), invalidPolicy);

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
        Assert.Contains("validation", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Exists Tests

    [Fact]
    public void Exists_AfterSave_ReturnsTrue()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        Assert.True(LkgStore.Exists());
    }

    [Fact]
    public void Exists_NoLkg_ReturnsFalse()
    {
        LkgStore.Delete();

        Assert.False(LkgStore.Exists());
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_ExistingLkg_RemovesFile()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);
        Assert.True(LkgStore.Exists());

        var result = LkgStore.Delete();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.False(LkgStore.Exists());
    }

    [Fact]
    public void Delete_NoLkg_ReturnsFalse()
    {
        LkgStore.Delete(); // Ensure clean state

        var result = LkgStore.Delete();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    #endregion

    #region GetMetadata Tests

    [Fact]
    public void GetMetadata_ValidLkg_ReturnsMetadata()
    {
        var policyJson = CreateValidPolicyJson(version: "3.2.1", ruleCount: 5);
        LkgStore.Save(policyJson, "C:\\test\\source.json");

        var result = LkgStore.GetMetadata();

        Assert.True(result.IsSuccess);
        var metadata = result.Value;
        Assert.True(metadata.Exists);
        Assert.False(metadata.IsCorrupt);
        Assert.Equal("3.2.1", metadata.PolicyVersion);
        Assert.Equal(5, metadata.RuleCount);
        Assert.Equal("C:\\test\\source.json", metadata.SourcePath);
    }

    [Fact]
    public void GetMetadata_NoLkg_ReturnsNotExists()
    {
        LkgStore.Delete();

        var result = LkgStore.GetMetadata();

        Assert.True(result.IsSuccess);
        var metadata = result.Value;
        Assert.False(metadata.Exists);
    }

    [Fact]
    public void GetMetadata_CorruptLkg_ReturnsIsCorrupt()
    {
        var policyJson = CreateValidPolicyJson();
        WriteLkgFile("wrong_checksum", policyJson);

        var result = LkgStore.GetMetadata();

        Assert.True(result.IsSuccess);
        var metadata = result.Value;
        Assert.True(metadata.Exists);
        Assert.True(metadata.IsCorrupt);
        Assert.NotNull(metadata.Error);
    }

    #endregion

    #region Checksum Tests

    [Fact]
    public void Load_TamperedPolicy_DetectsCorruption()
    {
        // Save a valid policy
        var originalPolicy = CreateValidPolicyJson(version: "1.0.0");
        LkgStore.Save(originalPolicy);

        // Tamper with the file by reading, modifying policy, and writing back
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        var wrapperJson = File.ReadAllText(lkgPath);
        var tamperedWrapper = wrapperJson.Replace("1.0.0", "9.9.9");
        File.WriteAllText(lkgPath, tamperedWrapper);

        // Load should detect corruption
        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
        Assert.Contains("checksum", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_SamePolicy_ProducesSameChecksum()
    {
        var policyJson = CreateValidPolicyJson();

        // Save twice and verify both produce valid loads
        LkgStore.Save(policyJson);
        var result1 = LkgStore.Load();

        LkgStore.Save(policyJson);
        var result2 = LkgStore.Load();

        Assert.True(result1.Exists);
        Assert.True(result2.Exists);
        Assert.Equal(result1.PolicyJson, result2.PolicyJson);
    }

    #endregion

    #region LkgLoadResult Tests

    [Fact]
    public void LkgLoadResult_Success_SetsAllProperties()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson, "C:\\source.json");

        var result = LkgStore.Load();

        Assert.True(result.Exists);
        Assert.NotNull(result.Policy);
        Assert.NotNull(result.PolicyJson);
        Assert.NotNull(result.SavedAt);
        Assert.Equal("C:\\source.json", result.SourcePath);
        Assert.Null(result.Error);
    }

    [Fact]
    public void LkgLoadResult_NotFound_SetsExistsFalse()
    {
        LkgStore.Delete();

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.Null(result.Policy);
        Assert.Null(result.PolicyJson);
        Assert.Null(result.SavedAt);
        Assert.Null(result.Error);
    }

    [Fact]
    public void LkgLoadResult_Failed_SetsError()
    {
        // Create corrupt LKG
        WriteLkgFile("bad", "not valid policy json");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
    }

    #endregion
}

/// <summary>
/// Integration tests for LKG IPC messages.
/// </summary>
public class LkgIpcMessageTests
{
    [Fact]
    public void ParseRequest_LkgShowRequest_Succeeds()
    {
        var json = """{ "type": "lkg-show" }""";

        var result = WfpTrafficControl.Shared.Ipc.IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WfpTrafficControl.Shared.Ipc.LkgShowRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_LkgRevertRequest_Succeeds()
    {
        var json = """{ "type": "lkg-revert" }""";

        var result = WfpTrafficControl.Shared.Ipc.IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WfpTrafficControl.Shared.Ipc.LkgRevertRequest>(result.Value);
    }

    [Fact]
    public void LkgShowResponse_NotFound_SerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgShowResponse.NotFound("C:\\ProgramData\\test.json");

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"exists\":false", json);
    }

    [Fact]
    public void LkgShowResponse_Found_SerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgShowResponse.Found(
            "1.0.0", 3, DateTime.UtcNow, "C:\\policy.json", "C:\\lkg.json");

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"exists\":true", json);
        Assert.Contains("\"policyVersion\":\"1.0.0\"", json);
        Assert.Contains("\"ruleCount\":3", json);
    }

    [Fact]
    public void LkgRevertResponse_Success_SerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgRevertResponse.Success(5, 2, 1, "1.0.0", 8);

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"lkgFound\":true", json);
        Assert.Contains("\"filtersCreated\":5", json);
        Assert.Contains("\"filtersRemoved\":2", json);
        Assert.Contains("\"rulesSkipped\":1", json);
    }

    [Fact]
    public void LkgRevertResponse_NotFound_SerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgRevertResponse.NotFound();

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"lkgFound\":false", json);
    }
}
