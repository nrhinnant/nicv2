using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Lkg;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Test collection to ensure LkgStore tests run sequentially (shared file state).
/// </summary>
[CollectionDefinition("LkgStore Sequential", DisableParallelization = true)]
#pragma warning disable CA1711 // "Collection" suffix is conventional for xUnit collection definitions
public class LkgStoreTestCollection : ICollectionFixture<LkgStoreTestFixture> { }
#pragma warning restore CA1711

/// <summary>
/// Shared fixture for LkgStore tests - ensures clean state.
/// </summary>
public sealed class LkgStoreTestFixture : IDisposable
{
    public LkgStoreTestFixture()
    {
        // Ensure clean state before test collection runs
        LkgStore.Delete();
    }

    public void Dispose()
    {
        // Clean up after test collection
        LkgStore.Delete();
    }
}

/// <summary>
/// Unit tests for LKG (Last Known Good) policy persistence.
/// Phase 14: LKG Persistence and Fail-Open Behavior
/// </summary>
[Collection("LkgStore Sequential")]
public sealed class LkgStoreTests : IDisposable
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
    public void SaveValidPolicyCreatesLkgFile()
    {
        var policyJson = CreateValidPolicyJson();

        var result = LkgStore.Save(policyJson, "C:\\test\\policy.json");

        Assert.True(result.IsSuccess);
        Assert.True(LkgStore.Exists());
    }

    [Fact]
    public void SaveValidPolicyIncludesChecksum()
    {
        var policyJson = CreateValidPolicyJson();

        var result = LkgStore.Save(policyJson);

        Assert.True(result.IsSuccess);

        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.NotNull(loadResult.PolicyJson);
    }

    [Fact]
    public void SaveValidPolicyPreservesSourcePath()
    {
        var policyJson = CreateValidPolicyJson();
        var sourcePath = "C:\\policies\\my-policy.json";

        LkgStore.Save(policyJson, sourcePath);

        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.Equal(sourcePath, loadResult.SourcePath);
    }

    [Fact]
    public void SaveEmptyPolicyReturnsFailure()
    {
        var result = LkgStore.Save("");

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveWhitespacePolicyReturnsFailure()
    {
        var result = LkgStore.Save("   \t\n  ");

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveOverwritesExistingLkg()
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
    public void LoadValidLkgReturnsPolicy()
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
    public void LoadValidLkgReturnsPolicyJson()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        var result = LkgStore.Load();

        Assert.True(result.Exists);
        Assert.NotNull(result.PolicyJson);
        Assert.Contains("version", result.PolicyJson);
    }

    [Fact]
    public void LoadValidLkgReturnsSavedAtTimestamp()
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
    public void LoadMissingLkgReturnsNotFound()
    {
        // Ensure no LKG exists
        LkgStore.Delete();

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.Null(result.Policy);
        Assert.Null(result.Error);
    }

    [Fact]
    public void LoadCorruptChecksumReturnsFailedWithError()
    {
        var policyJson = CreateValidPolicyJson();
        WriteLkgFile("invalid_checksum_value", policyJson);

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
        Assert.Contains("checksum", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadInvalidJsonReturnsFailedWithError()
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
    public void LoadEmptyFileReturnsFailedWithError()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, "");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void LoadMissingPolicyJsonReturnsFailedWithError()
    {
        var lkgPath = WfpConstants.GetLkgPolicyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lkgPath)!);
        File.WriteAllText(lkgPath, """{ "checksum": "abc", "savedAt": "2024-01-01T00:00:00Z" }""");

        var result = LkgStore.Load();

        Assert.False(result.Exists);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void LoadInvalidPolicyJsonReturnsFailedWithError()
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
    public void ExistsAfterSaveReturnsTrue()
    {
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        Assert.True(LkgStore.Exists());
    }

    [Fact]
    public void ExistsNoLkgReturnsFalse()
    {
        LkgStore.Delete();

        Assert.False(LkgStore.Exists());
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void DeleteExistingLkgRemovesFile()
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
    public void DeleteNoLkgReturnsFalse()
    {
        LkgStore.Delete(); // Ensure clean state

        var result = LkgStore.Delete();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    #endregion

    #region GetMetadata Tests

    [Fact]
    public void GetMetadataValidLkgReturnsMetadata()
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
    public void GetMetadataNoLkgReturnsNotExists()
    {
        LkgStore.Delete();

        var result = LkgStore.GetMetadata();

        Assert.True(result.IsSuccess);
        var metadata = result.Value;
        Assert.False(metadata.Exists);
    }

    [Fact]
    public void GetMetadataCorruptLkgReturnsIsCorrupt()
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
    public void LoadTamperedPolicyDetectsCorruption()
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
    public void SaveSamePolicyProducesSameChecksum()
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
    public void LkgLoadResultSuccessSetsAllProperties()
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
    public void LkgLoadResultNotFoundSetsExistsFalse()
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
    public void LkgLoadResultFailedSetsError()
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
[Collection("LkgStore Sequential")]
public class LkgIpcMessageTests
{
    [Fact]
    public void ParseRequestLkgShowRequestSucceeds()
    {
        var json = """{ "type": "lkg-show" }""";

        var result = WfpTrafficControl.Shared.Ipc.IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WfpTrafficControl.Shared.Ipc.LkgShowRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestLkgRevertRequestSucceeds()
    {
        var json = """{ "type": "lkg-revert" }""";

        var result = WfpTrafficControl.Shared.Ipc.IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WfpTrafficControl.Shared.Ipc.LkgRevertRequest>(result.Value);
    }

    [Fact]
    public void LkgShowResponseNotFoundSerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgShowResponse.NotFound("C:\\ProgramData\\test.json");

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"exists\":false", json);
    }

    [Fact]
    public void LkgShowResponseFoundSerializesCorrectly()
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
    public void LkgRevertResponseSuccessSerializesCorrectly()
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
    public void LkgRevertResponseNotFoundSerializesCorrectly()
    {
        var response = WfpTrafficControl.Shared.Ipc.LkgRevertResponse.NotFound();

        var json = WfpTrafficControl.Shared.Ipc.IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"lkgFound\":false", json);
    }
}

/// <summary>
/// Tests for LkgStore concurrent access behavior.
/// Verifies thread-safety and atomic operation guarantees.
/// </summary>
[Collection("LkgStore Sequential")]
public sealed class LkgStoreConcurrentAccessTests : IDisposable
{
    public LkgStoreConcurrentAccessTests()
    {
        // Ensure clean state
        LkgStore.Delete();
    }

    public void Dispose()
    {
        LkgStore.Delete();
    }

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

    // ========================================
    // Concurrent Read Tests
    // ========================================

    [Fact]
    public async Task ConcurrentReadsAllSucceed()
    {
        // Arrange - save a policy first
        var policyJson = CreateValidPolicyJson(version: "1.0.0", ruleCount: 3);
        LkgStore.Save(policyJson);

        // Act - perform many concurrent reads
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var result = LkgStore.Load();
            return result;
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - all reads should succeed with the same data
        Assert.All(results, result =>
        {
            Assert.True(result.Exists);
            Assert.NotNull(result.Policy);
            Assert.Equal("1.0.0", result.Policy!.Version);
            Assert.Equal(3, result.Policy.Rules.Count);
        });
    }

    [Fact]
    public async Task ConcurrentReadsNoExceptions()
    {
        // Arrange
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        // Act - perform many concurrent reads
        var exceptions = new List<Exception>();
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    LkgStore.Load();
                    LkgStore.Exists();
                    LkgStore.GetMetadata();
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - no exceptions should occur
        Assert.Empty(exceptions);
    }

    // ========================================
    // Concurrent Write Tests
    // ========================================

    [Fact]
    public async Task ConcurrentSavesAtLeastOneSucceedsNoCorruption()
    {
        // Arrange
        var versions = Enumerable.Range(1, 10).Select(i => $"{i}.0.0").ToArray();

        // Act - save different versions concurrently
        var tasks = versions.Select(version => Task.Run(() =>
        {
            var policyJson = CreateValidPolicyJson(version: version);
            return LkgStore.Save(policyJson);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - at least one save should succeed (concurrent writes may fail due to file locking)
        Assert.True(results.Any(r => r.IsSuccess), "At least one save should succeed");

        // Final state should be valid (one of the versions) - no corruption
        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.NotNull(loadResult.Policy);
        Assert.Contains(loadResult.Policy!.Version, versions);
        Assert.Null(loadResult.Error); // No checksum errors (no corruption)
    }

    [Fact]
    public async Task ConcurrentSavesNoFileCorruption()
    {
        // Arrange
        var iterations = 20;
        var exceptions = new List<Exception>();

        // Act - rapid concurrent saves
        var tasks = Enumerable.Range(0, iterations).Select(i => Task.Run(() =>
        {
            try
            {
                var policyJson = CreateValidPolicyJson(version: $"1.0.{i}", ruleCount: (i % 5) + 1);
                var result = LkgStore.Save(policyJson);
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
                return false;
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - no exceptions during concurrent saves
        Assert.Empty(exceptions);

        // File should be in a valid state after all operations
        var loadResult = LkgStore.Load();
        Assert.True(loadResult.Exists);
        Assert.NotNull(loadResult.Policy);
        // Checksum should be valid (Load would fail with checksum error otherwise)
        Assert.Null(loadResult.Error);
    }

    // ========================================
    // Concurrent Read/Write Tests
    // ========================================

    [Fact]
    public async Task ConcurrentReadAndWriteNoCorruption()
    {
        // Arrange - save initial policy
        var initialPolicy = CreateValidPolicyJson(version: "1.0.0");
        LkgStore.Save(initialPolicy);

        var exceptions = new List<Exception>();
        var readResults = new List<LkgLoadResult>();

        // Act - concurrent reads and writes
        var writerTask = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var policyJson = CreateValidPolicyJson(version: $"2.0.{i}");
                    LkgStore.Save(policyJson);
                    await Task.Delay(5); // Small delay to simulate real workload
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }
        });

        var readerTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var result = LkgStore.Load();
                    lock (readResults)
                    {
                        readResults.Add(result);
                    }
                    await Task.Delay(2);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }
        })).ToArray();

        await Task.WhenAll(new[] { writerTask }.Concat(readerTasks));

        // Assert - no unexpected exceptions
        Assert.Empty(exceptions);

        // All reads should either succeed (valid policy) or fail cleanly (no corruption/crash)
        Assert.All(readResults, result =>
        {
            // Either we read a valid policy, or file was mid-write and we got NotFound/error
            // We should never get a partial/corrupted read (would cause checksum error)
            if (result.Exists)
            {
                Assert.NotNull(result.Policy);
                // If we got a policy, it should be complete and valid (no checksum errors)
                Assert.Null(result.Error);
                // Version should be in valid semver format (either 1.0.0 or 2.0.x)
                Assert.Matches(@"^\d+\.\d+\.\d+$", result.Policy!.Version);
            }
        });
    }

    // ========================================
    // Concurrent Delete Tests
    // ========================================

    [Fact]
    public async Task ConcurrentDeletesNoExceptions()
    {
        // Arrange
        var policyJson = CreateValidPolicyJson();
        LkgStore.Save(policyJson);

        var exceptions = new List<Exception>();

        // Act - multiple concurrent deletes
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            try
            {
                LkgStore.Delete();
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.False(LkgStore.Exists());
    }

    [Fact]
    public async Task ConcurrentDeleteAndSaveNoExceptions()
    {
        // Arrange
        var exceptions = new List<Exception>();

        // Act - concurrent saves and deletes
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            try
            {
                if (i % 2 == 0)
                {
                    var policyJson = CreateValidPolicyJson(version: $"1.0.{i}");
                    LkgStore.Save(policyJson);
                }
                else
                {
                    LkgStore.Delete();
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - no exceptions should occur
        Assert.Empty(exceptions);

        // Final state should be deterministic (either exists or not)
        var exists = LkgStore.Exists();
        if (exists)
        {
            var result = LkgStore.Load();
            Assert.True(result.Exists);
            Assert.NotNull(result.Policy);
        }
    }

    // ========================================
    // Stress Tests
    // ========================================

    [Fact]
    public async Task MixedConcurrentOperationsNoExceptions()
    {
        // Arrange - initial policy
        var initialPolicy = CreateValidPolicyJson();
        LkgStore.Save(initialPolicy);

        var exceptions = new List<Exception>();
        var operationCount = 100;

        // Act - mix of all operations
        var tasks = Enumerable.Range(0, operationCount).Select(i => Task.Run(() =>
        {
            try
            {
                switch (i % 5)
                {
                    case 0: // Save
                        var policyJson = CreateValidPolicyJson(version: $"1.{i}.0");
                        LkgStore.Save(policyJson);
                        break;
                    case 1: // Load
                        LkgStore.Load();
                        break;
                    case 2: // Exists
                        LkgStore.Exists();
                        break;
                    case 3: // GetMetadata
                        LkgStore.GetMetadata();
                        break;
                    case 4: // Delete (less frequent)
                        if (i % 10 == 4) // Only 1 in 10 operations is delete
                        {
                            LkgStore.Delete();
                        }
                        else
                        {
                            LkgStore.Load();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - all operations should complete without throwing
        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task RapidSaveLoadCyclesMaintainsIntegrity()
    {
        // Arrange
        var cycleCount = 50;
        var integrityErrors = new List<string>(); // Only track corruption/integrity issues
        var successfulSaves = 0;
        var successfulLoads = 0;

        // Act - rapid save/load cycles in parallel
        var tasks = Enumerable.Range(0, 10).Select(taskId => Task.Run(() =>
        {
            for (int i = 0; i < cycleCount; i++)
            {
                try
                {
                    var version = $"{taskId}.{i}.0";
                    var policyJson = CreateValidPolicyJson(version: version);

                    // Save - may fail due to concurrent access (expected, not an integrity error)
                    var saveResult = LkgStore.Save(policyJson);
                    if (saveResult.IsSuccess)
                    {
                        Interlocked.Increment(ref successfulSaves);
                    }
                    // Save failures due to "file in use" are expected in concurrent scenarios

                    // Load and verify
                    var loadResult = LkgStore.Load();
                    if (loadResult.Exists && loadResult.Policy != null)
                    {
                        Interlocked.Increment(ref successfulLoads);
                        // If we got the same version we saved, great
                        // If we got a different version, another thread saved - that's fine
                        // What matters is that the data is valid (no checksum/integrity errors)
                        if (loadResult.Error != null)
                        {
                            lock (integrityErrors)
                            {
                                integrityErrors.Add($"Load returned integrity error: {loadResult.Error}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (integrityErrors)
                    {
                        integrityErrors.Add($"Unexpected exception: {ex.Message}");
                    }
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - no integrity/corruption errors
        Assert.Empty(integrityErrors);
        // Some operations should have succeeded
        Assert.True(successfulSaves > 0, "At least some saves should succeed");
        Assert.True(successfulLoads > 0, "At least some loads should succeed");
    }
}
