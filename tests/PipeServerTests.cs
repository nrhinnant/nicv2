// tests/PipeServerTests.cs
// Unit tests for PipeServer request handlers
// Addresses critical test gap: PipeServer handlers have 0% coverage

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WfpTrafficControl.Service;
using WfpTrafficControl.Service.Ipc;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Audit;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Lkg;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Mock implementation of IWfpEngine for testing PipeServer handlers.
/// </summary>
public class MockWfpEngineForPipeServer : IWfpEngine
{
    // State
    public bool ProviderExistsValue { get; set; }
    public bool SublayerExistsValue { get; set; }
    public bool DemoBlockFilterExistsValue { get; set; }
    public int FilterCount { get; set; }

    // Error injection
    public Error? EnsureProviderAndSublayerExistError { get; set; }
    public Error? RemoveProviderAndSublayerError { get; set; }
    public Error? AddDemoBlockFilterError { get; set; }
    public Error? RemoveDemoBlockFilterError { get; set; }
    public Error? RemoveAllFiltersError { get; set; }
    public Error? ApplyFiltersError { get; set; }

    // Call tracking
    public int EnsureProviderAndSublayerExistCallCount { get; private set; }
    public int RemoveProviderAndSublayerCallCount { get; private set; }
    public int AddDemoBlockFilterCallCount { get; private set; }
    public int RemoveDemoBlockFilterCallCount { get; private set; }
    public int RemoveAllFiltersCallCount { get; private set; }
    public int ApplyFiltersCallCount { get; private set; }
    public List<CompiledFilter>? LastAppliedFilters { get; private set; }

    // ApplyFilters result configuration
    public ApplyResult ApplyFiltersResult { get; set; } = new ApplyResult
    {
        FiltersCreated = 1,
        FiltersRemoved = 0,
        FiltersUnchanged = 0
    };

    public Result EnsureProviderAndSublayerExist()
    {
        EnsureProviderAndSublayerExistCallCount++;
        if (EnsureProviderAndSublayerExistError != null)
            return Result.Failure(EnsureProviderAndSublayerExistError.Code, EnsureProviderAndSublayerExistError.Message);

        ProviderExistsValue = true;
        SublayerExistsValue = true;
        return Result.Success();
    }

    public Result RemoveProviderAndSublayer()
    {
        RemoveProviderAndSublayerCallCount++;
        if (RemoveProviderAndSublayerError != null)
            return Result.Failure(RemoveProviderAndSublayerError.Code, RemoveProviderAndSublayerError.Message);

        ProviderExistsValue = false;
        SublayerExistsValue = false;
        return Result.Success();
    }

    public Result<bool> ProviderExists()
    {
        return Result<bool>.Success(ProviderExistsValue);
    }

    public Result<bool> SublayerExists()
    {
        return Result<bool>.Success(SublayerExistsValue);
    }

    public Result AddDemoBlockFilter()
    {
        AddDemoBlockFilterCallCount++;
        if (AddDemoBlockFilterError != null)
            return Result.Failure(AddDemoBlockFilterError.Code, AddDemoBlockFilterError.Message);

        DemoBlockFilterExistsValue = true;
        return Result.Success();
    }

    public Result RemoveDemoBlockFilter()
    {
        RemoveDemoBlockFilterCallCount++;
        if (RemoveDemoBlockFilterError != null)
            return Result.Failure(RemoveDemoBlockFilterError.Code, RemoveDemoBlockFilterError.Message);

        DemoBlockFilterExistsValue = false;
        return Result.Success();
    }

    public Result<bool> DemoBlockFilterExists()
    {
        return Result<bool>.Success(DemoBlockFilterExistsValue);
    }

    public Result<int> RemoveAllFilters()
    {
        RemoveAllFiltersCallCount++;
        if (RemoveAllFiltersError != null)
            return Result<int>.Failure(RemoveAllFiltersError.Code, RemoveAllFiltersError.Message);

        var count = FilterCount;
        FilterCount = 0;
        return Result<int>.Success(count);
    }

    public Result<ApplyResult> ApplyFilters(List<CompiledFilter> filters)
    {
        ApplyFiltersCallCount++;
        LastAppliedFilters = filters;
        if (ApplyFiltersError != null)
            return Result<ApplyResult>.Failure(ApplyFiltersError.Code, ApplyFiltersError.Message);

        FilterCount = filters?.Count ?? 0;
        return Result<ApplyResult>.Success(ApplyFiltersResult);
    }

    public void Reset()
    {
        ProviderExistsValue = false;
        SublayerExistsValue = false;
        DemoBlockFilterExistsValue = false;
        FilterCount = 0;
        EnsureProviderAndSublayerExistError = null;
        RemoveProviderAndSublayerError = null;
        AddDemoBlockFilterError = null;
        RemoveDemoBlockFilterError = null;
        RemoveAllFiltersError = null;
        ApplyFiltersError = null;
        EnsureProviderAndSublayerExistCallCount = 0;
        RemoveProviderAndSublayerCallCount = 0;
        AddDemoBlockFilterCallCount = 0;
        RemoveDemoBlockFilterCallCount = 0;
        RemoveAllFiltersCallCount = 0;
        ApplyFiltersCallCount = 0;
        LastAppliedFilters = null;
    }
}

/// <summary>
/// Mock implementation of IAuditLogWriter for testing.
/// </summary>
public class MockAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogEntry> Entries { get; } = new();
    public string LogPath => "C:\\mock\\audit.log";

    public void Write(AuditLogEntry entry)
    {
        if (entry != null)
        {
            Entries.Add(entry);
        }
    }

    public void Clear() => Entries.Clear();

    public bool HasEvent(string eventName)
    {
        return Entries.Any(e => e.Event == eventName);
    }

    public AuditLogEntry? GetLastEntry()
    {
        return Entries.LastOrDefault();
    }
}


/// <summary>
/// Unit tests for PipeServer.ProcessBootstrapRequest().
/// </summary>
public class PipeServerBootstrapTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerBootstrapTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessBootstrapRequestSuccessReturnsOk()
    {
        // Arrange
        var request = new BootstrapRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<BootstrapResponse>(response);
        var bootstrapResponse = (BootstrapResponse)response;
        Assert.True(bootstrapResponse.Ok);
        Assert.Equal(1, _mockEngine.EnsureProviderAndSublayerExistCallCount);
    }

    [Fact]
    public void ProcessBootstrapRequestEngineErrorReturnsFailure()
    {
        // Arrange
        _mockEngine.EnsureProviderAndSublayerExistError = new Error(ErrorCodes.WfpError, "WFP error");
        var request = new BootstrapRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<BootstrapResponse>(response);
        var bootstrapResponse = (BootstrapResponse)response;
        Assert.False(bootstrapResponse.Ok);
        Assert.Contains("WFP error", bootstrapResponse.Error);
    }

    [Fact]
    public void ProcessBootstrapRequestReportsProviderAndSublayerState()
    {
        // Arrange
        _mockEngine.ProviderExistsValue = true;
        _mockEngine.SublayerExistsValue = true;
        var request = new BootstrapRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var bootstrapResponse = (BootstrapResponse)response;
        Assert.True(bootstrapResponse.Ok);
        Assert.True(bootstrapResponse.ProviderExists);
        Assert.True(bootstrapResponse.SublayerExists);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        // Use reflection to call the private ProcessRequest method
        // This is necessary since PipeServer handlers are private
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Create a minimal PipeServer instance
        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessTeardownRequest().
/// </summary>
public class PipeServerTeardownTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerTeardownTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessTeardownRequestSuccessReturnsOk()
    {
        // Arrange
        _mockEngine.ProviderExistsValue = true;
        _mockEngine.SublayerExistsValue = true;
        var request = new TeardownRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<TeardownResponse>(response);
        var teardownResponse = (TeardownResponse)response;
        Assert.True(teardownResponse.Ok);
        Assert.Equal(1, _mockEngine.RemoveProviderAndSublayerCallCount);
    }

    [Fact]
    public void ProcessTeardownRequestWritesAuditLog()
    {
        // Arrange
        _mockEngine.ProviderExistsValue = true;
        _mockEngine.SublayerExistsValue = true;
        var request = new TeardownRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("teardown-started"));
        Assert.True(_mockAuditLog.HasEvent("teardown-finished"));
    }

    [Fact]
    public void ProcessTeardownRequestEngineErrorReturnsFailure()
    {
        // Arrange
        _mockEngine.RemoveProviderAndSublayerError = new Error(ErrorCodes.WfpError, "Cannot remove");
        var request = new TeardownRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var teardownResponse = (TeardownResponse)response;
        Assert.False(teardownResponse.Ok);
        Assert.Contains("Cannot remove", teardownResponse.Error);
    }

    [Fact]
    public void ProcessTeardownRequestFailureWritesFailedAuditEntry()
    {
        // Arrange
        _mockEngine.RemoveProviderAndSublayerError = new Error(ErrorCodes.WfpError, "Error");
        var request = new TeardownRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("teardown-started"));
        Assert.True(_mockAuditLog.HasEvent("teardown-finished"));
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessDemoBlock* requests.
/// </summary>
public class PipeServerDemoBlockTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerDemoBlockTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessDemoBlockEnableRequestSuccessReturnsOk()
    {
        // Arrange
        var request = new DemoBlockEnableRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<DemoBlockEnableResponse>(response);
        var demoResponse = (DemoBlockEnableResponse)response;
        Assert.True(demoResponse.Ok);
        Assert.True(demoResponse.FilterEnabled);
        Assert.Equal(1, _mockEngine.AddDemoBlockFilterCallCount);
    }

    [Fact]
    public void ProcessDemoBlockEnableRequestBootstrapFirst()
    {
        // Arrange
        var request = new DemoBlockEnableRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert - should bootstrap before adding demo filter
        Assert.Equal(1, _mockEngine.EnsureProviderAndSublayerExistCallCount);
        Assert.Equal(1, _mockEngine.AddDemoBlockFilterCallCount);
    }

    [Fact]
    public void ProcessDemoBlockEnableRequestBootstrapFailsReturnsError()
    {
        // Arrange
        _mockEngine.EnsureProviderAndSublayerExistError = new Error(ErrorCodes.WfpError, "Bootstrap failed");
        var request = new DemoBlockEnableRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var demoResponse = (DemoBlockEnableResponse)response;
        Assert.False(demoResponse.Ok);
        Assert.Contains("Bootstrap failed", demoResponse.Error);
        Assert.Equal(0, _mockEngine.AddDemoBlockFilterCallCount);
    }

    [Fact]
    public void ProcessDemoBlockDisableRequestSuccessReturnsOk()
    {
        // Arrange
        _mockEngine.DemoBlockFilterExistsValue = true;
        var request = new DemoBlockDisableRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<DemoBlockDisableResponse>(response);
        var demoResponse = (DemoBlockDisableResponse)response;
        Assert.True(demoResponse.Ok);
        Assert.True(demoResponse.FilterDisabled);
        Assert.Equal(1, _mockEngine.RemoveDemoBlockFilterCallCount);
    }

    [Fact]
    public void ProcessDemoBlockStatusRequestFilterExistsReturnsActive()
    {
        // Arrange
        _mockEngine.DemoBlockFilterExistsValue = true;
        var request = new DemoBlockStatusRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<DemoBlockStatusResponse>(response);
        var statusResponse = (DemoBlockStatusResponse)response;
        Assert.True(statusResponse.Ok);
        Assert.True(statusResponse.FilterActive);
    }

    [Fact]
    public void ProcessDemoBlockStatusRequestFilterNotExistsReturnsInactive()
    {
        // Arrange
        _mockEngine.DemoBlockFilterExistsValue = false;
        var request = new DemoBlockStatusRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var statusResponse = (DemoBlockStatusResponse)response;
        Assert.True(statusResponse.Ok);
        Assert.False(statusResponse.FilterActive);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessRollbackRequest().
/// </summary>
public class PipeServerRollbackTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerRollbackTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessRollbackRequestSuccessReturnsFilterCount()
    {
        // Arrange
        _mockEngine.FilterCount = 5;
        var request = new RollbackRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<RollbackResponse>(response);
        var rollbackResponse = (RollbackResponse)response;
        Assert.True(rollbackResponse.Ok);
        Assert.Equal(5, rollbackResponse.FiltersRemoved);
        Assert.Equal(1, _mockEngine.RemoveAllFiltersCallCount);
    }

    [Fact]
    public void ProcessRollbackRequestWritesAuditLog()
    {
        // Arrange
        _mockEngine.FilterCount = 3;
        var request = new RollbackRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("rollback-started"));
        Assert.True(_mockAuditLog.HasEvent("rollback-finished"));
    }

    [Fact]
    public void ProcessRollbackRequestFailureWritesFailedAuditEntry()
    {
        // Arrange
        _mockEngine.RemoveAllFiltersError = new Error(ErrorCodes.WfpError, "Rollback failed");
        var request = new RollbackRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("rollback-started"));
        Assert.True(_mockAuditLog.HasEvent("rollback-finished"));
    }

    [Fact]
    public void ProcessRollbackRequestEngineErrorReturnsFailure()
    {
        // Arrange
        _mockEngine.RemoveAllFiltersError = new Error(ErrorCodes.WfpError, "WFP error");
        var request = new RollbackRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var rollbackResponse = (RollbackResponse)response;
        Assert.False(rollbackResponse.Ok);
        Assert.Contains("WFP error", rollbackResponse.Error);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessValidateRequest().
/// </summary>
public class PipeServerValidateTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerValidateTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessValidateRequestValidPolicyReturnsOk()
    {
        // Arrange
        var validPolicyJson = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        var request = new ValidateRequest { PolicyJson = validPolicyJson };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<ValidateResponse>(response);
        var validateResponse = (ValidateResponse)response;
        Assert.True(validateResponse.Ok);
        Assert.True(validateResponse.Valid);
    }

    [Fact]
    public void ProcessValidateRequestInvalidPolicyReturnsValidationErrors()
    {
        // Arrange
        var invalidPolicyJson = """
        {
            "version": "invalid",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        var request = new ValidateRequest { PolicyJson = invalidPolicyJson };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var validateResponse = (ValidateResponse)response;
        Assert.True(validateResponse.Ok); // Request succeeded
        Assert.False(validateResponse.Valid); // But policy is invalid
        Assert.True(validateResponse.Errors.Count > 0);
    }

    [Fact]
    public void ProcessValidateRequestInvalidJsonReturnsError()
    {
        // Arrange
        var request = new ValidateRequest { PolicyJson = "not valid json" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var validateResponse = (ValidateResponse)response;
        Assert.True(validateResponse.Ok);
        Assert.False(validateResponse.Valid);
    }

    [Fact]
    public void ProcessValidateRequestValidPolicyWithRulesReturnsRuleCount()
    {
        // Arrange
        var policyWithRules = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "rule1",
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "priority": 100,
                    "enabled": true
                },
                {
                    "id": "rule2",
                    "action": "allow",
                    "direction": "inbound",
                    "protocol": "tcp",
                    "priority": 200,
                    "enabled": true
                }
            ]
        }
        """;
        var request = new ValidateRequest { PolicyJson = policyWithRules };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var validateResponse = (ValidateResponse)response;
        Assert.True(validateResponse.Valid);
        Assert.Equal(2, validateResponse.RuleCount);
        Assert.Equal("1.0.0", validateResponse.Version);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessApplyRequest() - security-critical tests.
/// </summary>
public sealed class PipeServerApplySecurityTests : IDisposable
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;
    private readonly string _testDirectory;

    public PipeServerApplySecurityTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PipeServerApplyTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void ProcessApplyRequestEmptyPathReturnsError()
    {
        // Arrange
        var request = new ApplyRequest { PolicyPath = "" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("required", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessApplyRequestPathTraversalReturnsError()
    {
        // Arrange - path traversal attack using explicit .. in path
        var request = new ApplyRequest { PolicyPath = "test..path" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert - should be rejected by the Contains("..") check
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("traversal", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessApplyRequestPathWithDotDotSequenceReturnsError()
    {
        // Arrange - path with .. sequence
        var request = new ApplyRequest { PolicyPath = @"C:\policies\.." };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert - should be rejected by the Contains("..") check
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("traversal", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessApplyRequestPathWithEmbeddedDotDotReturnsError()
    {
        // Arrange - path with embedded ..
        var request = new ApplyRequest { PolicyPath = @"policy..json" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert - should be rejected by the Contains("..") check
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("traversal", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessApplyRequestFileNotFoundReturnsError()
    {
        // Arrange
        var request = new ApplyRequest { PolicyPath = @"C:\nonexistent\policy.json" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        // Error message is "Failed to read policy file: Could not find file..."
        Assert.Contains("could not find", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessApplyRequestValidFileAppliesPolicy()
    {
        // Arrange
        var policyPath = Path.Combine(_testDirectory, "test-policy.json");
        var policyContent = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "test-rule",
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "priority": 100,
                    "enabled": true
                }
            ]
        }
        """;
        File.WriteAllText(policyPath, policyContent);
        var request = new ApplyRequest { PolicyPath = policyPath };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var applyResponse = (ApplyResponse)response;
        Assert.True(applyResponse.Ok, applyResponse.Error);
        Assert.Equal(1, _mockEngine.ApplyFiltersCallCount);
    }

    [Fact]
    public void ProcessApplyRequestWritesAuditLog()
    {
        // Arrange
        var policyPath = Path.Combine(_testDirectory, "audit-test.json");
        var policyContent = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        File.WriteAllText(policyPath, policyContent);
        var request = new ApplyRequest { PolicyPath = policyPath };

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("apply-started"));
        Assert.True(_mockAuditLog.HasEvent("apply-finished"));
    }

    [Fact]
    public void ProcessApplyRequestInvalidPolicyReturnsValidationError()
    {
        // Arrange
        var policyPath = Path.Combine(_testDirectory, "invalid-policy.json");
        var invalidPolicy = """
        {
            "version": "bad-version",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        File.WriteAllText(policyPath, invalidPolicy);
        var request = new ApplyRequest { PolicyPath = policyPath };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("validation", applyResponse.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _mockEngine.ApplyFiltersCallCount);
    }

    [Fact]
    public void ProcessApplyRequestEngineFailureReturnsError()
    {
        // Arrange
        var policyPath = Path.Combine(_testDirectory, "engine-fail.json");
        var policyContent = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        File.WriteAllText(policyPath, policyContent);
        _mockEngine.ApplyFiltersError = new Error(ErrorCodes.WfpError, "WFP apply failed");
        var request = new ApplyRequest { PolicyPath = policyPath };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var applyResponse = (ApplyResponse)response;
        Assert.False(applyResponse.Ok);
        Assert.Contains("WFP apply failed", applyResponse.Error);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessLkgShowRequest() and ProcessLkgRevertRequest().
/// </summary>
[Collection("LkgStore Sequential")]
public sealed class PipeServerLkgTests : IDisposable
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;
    private readonly string _testDirectory;

    public PipeServerLkgTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PipeServerLkgTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up LKG if created
        try
        {
            LkgStore.Delete();
        }
        catch { /* Ignore */ }

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void ProcessLkgShowRequestNoLkgReturnsNotFound()
    {
        // Arrange
        LkgStore.Delete(); // Ensure no LKG
        var request = new LkgShowRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<LkgShowResponse>(response);
        var lkgResponse = (LkgShowResponse)response;
        Assert.True(lkgResponse.Ok);
        Assert.False(lkgResponse.Exists);
    }

    [Fact]
    public void ProcessLkgShowRequestLkgExistsReturnsMetadata()
    {
        // Arrange - ensure clean state first
        LkgStore.Delete();
        var policyJson = """
        {
            "version": "2.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                { "id": "rule1", "action": "block", "direction": "outbound", "protocol": "tcp", "priority": 100, "enabled": true }
            ]
        }
        """;
        LkgStore.Save(policyJson, "C:\\test\\policy.json");
        var request = new LkgShowRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var lkgResponse = (LkgShowResponse)response;
        Assert.True(lkgResponse.Ok);
        Assert.True(lkgResponse.Exists);
        Assert.Equal("2.0.0", lkgResponse.PolicyVersion);
        Assert.Equal(1, lkgResponse.RuleCount);
    }

    [Fact]
    public void ProcessLkgRevertRequestNoLkgReturnsNotFound()
    {
        // Arrange - ensure no LKG exists by deleting and verifying
        LkgStore.Delete();
        Assert.False(LkgStore.Exists(), "LKG should not exist after delete");
        var request = new LkgRevertRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<LkgRevertResponse>(response);
        var revertResponse = (LkgRevertResponse)response;
        Assert.False(revertResponse.Ok, $"Expected Ok=false but got Ok=true. LkgFound={revertResponse.LkgFound}, Error={revertResponse.Error}");
        Assert.False(revertResponse.LkgFound);
    }

    [Fact]
    public void ProcessLkgRevertRequestValidLkgAppliesPolicy()
    {
        // Arrange
        var policyJson = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                { "id": "rule1", "action": "block", "direction": "outbound", "protocol": "tcp", "priority": 100, "enabled": true }
            ]
        }
        """;
        LkgStore.Save(policyJson);
        var request = new LkgRevertRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var revertResponse = (LkgRevertResponse)response;
        Assert.True(revertResponse.Ok, revertResponse.Error);
        Assert.True(revertResponse.LkgFound);
        Assert.Equal(1, _mockEngine.ApplyFiltersCallCount);
    }

    [Fact]
    public void ProcessLkgRevertRequestWritesAuditLog()
    {
        // Arrange
        var policyJson = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        LkgStore.Save(policyJson);
        var request = new LkgRevertRequest();

        // Act
        InvokeProcessRequest(request);

        // Assert
        Assert.True(_mockAuditLog.HasEvent("lkg-revert-started"));
        Assert.True(_mockAuditLog.HasEvent("lkg-revert-finished"));
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessWatchSetRequest() and ProcessWatchStatusRequest().
/// </summary>
public sealed class PipeServerWatchTests : IDisposable
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;
    private readonly string _testDirectory;

    public PipeServerWatchTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PipeServerWatchTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void ProcessWatchStatusRequestNotWatchingReturnsCorrectState()
    {
        // Arrange
        var request = new WatchStatusRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<WatchStatusResponse>(response);
        var statusResponse = (WatchStatusResponse)response;
        Assert.True(statusResponse.Ok);
        Assert.False(statusResponse.Watching);
        Assert.Null(statusResponse.PolicyPath);
    }

    [Fact]
    public void ProcessWatchSetRequestEmptyPathDisablesWatching()
    {
        // Arrange
        var request = new WatchSetRequest { PolicyPath = "" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var watchResponse = (WatchSetResponse)response;
        Assert.True(watchResponse.Ok);
        Assert.False(watchResponse.Watching);
    }

    [Fact]
    public void ProcessWatchSetRequestPathTraversalReturnsError()
    {
        // Arrange - path with .. sequence triggers traversal check
        var request = new WatchSetRequest { PolicyPath = "policy..json" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert - should be rejected by the Contains("..") check
        var watchResponse = (WatchSetResponse)response;
        Assert.False(watchResponse.Ok);
        Assert.Contains("traversal", watchResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessWatchSetRequestFileNotFoundReturnsError()
    {
        // Arrange
        var request = new WatchSetRequest { PolicyPath = @"C:\nonexistent\policy.json" };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var watchResponse = (WatchSetResponse)response;
        Assert.False(watchResponse.Ok);
        Assert.Contains("not found", watchResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessWatchSetRequestValidFileEnablesWatching()
    {
        // Arrange
        var policyPath = Path.Combine(_testDirectory, "watch-policy.json");
        var policyContent = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;
        File.WriteAllText(policyPath, policyContent);
        var request = new WatchSetRequest { PolicyPath = policyPath };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var watchResponse = (WatchSetResponse)response;
        Assert.True(watchResponse.Ok, watchResponse.Error);
        Assert.True(watchResponse.Watching);
        Assert.Equal(policyPath, watchResponse.PolicyPath);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessAuditLogsRequest().
/// </summary>
public class PipeServerAuditLogsTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerAuditLogsTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessAuditLogsRequestDefaultTailReturnsEntries()
    {
        // Arrange
        var request = new AuditLogsRequest();

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        Assert.IsType<AuditLogsResponse>(response);
        var auditResponse = (AuditLogsResponse)response;
        Assert.True(auditResponse.Ok);
    }

    [Fact]
    public void ProcessAuditLogsRequestWithTailReturnsTailEntries()
    {
        // Arrange
        var request = new AuditLogsRequest { Tail = 5 };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var auditResponse = (AuditLogsResponse)response;
        Assert.True(auditResponse.Ok);
    }

    [Fact]
    public void ProcessAuditLogsRequestWithSinceMinutesReturnsRecentEntries()
    {
        // Arrange
        var request = new AuditLogsRequest { SinceMinutes = 30 };

        // Act
        var response = InvokeProcessRequest(request);

        // Assert
        var auditResponse = (AuditLogsResponse)response;
        Assert.True(auditResponse.Ok);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PipeServer.ProcessPingRequest().
/// </summary>
public class PipeServerPingTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerPingTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessPingRequestReturnsServiceVersion()
    {
        // Arrange
        var request = new PingRequest();

        // Act
        var response = InvokeProcessRequest(request, serviceVersion: "2.5.0");

        // Assert
        Assert.IsType<PingResponse>(response);
        var pingResponse = (PingResponse)response;
        Assert.True(pingResponse.Ok);
        Assert.Equal("2.5.0", pingResponse.ServiceVersion);
    }

    [Fact]
    public void ProcessPingRequestIncludesProtocolVersion()
    {
        // Arrange
        var request = new PingRequest();

        // Act
        var response = InvokeProcessRequest(request, serviceVersion: "1.0.0");

        // Assert
        var pingResponse = (PingResponse)response;
        Assert.Equal(WfpConstants.IpcProtocolVersion, pingResponse.ProtocolVersion);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request, string serviceVersion = "1.0.0")
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            serviceVersion,
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for unknown request type handling.
/// </summary>
public class PipeServerUnknownRequestTests
{
    private readonly MockWfpEngineForPipeServer _mockEngine;
    private readonly MockAuditLogWriter _mockAuditLog;

    public PipeServerUnknownRequestTests()
    {
        _mockEngine = new MockWfpEngineForPipeServer();
        _mockAuditLog = new MockAuditLogWriter();
    }

    [Fact]
    public void ProcessRequestUnknownTypeReturnsError()
    {
        // Arrange - create a custom request with unknown type
        var unknownRequest = new UnknownTestRequest();

        // Act
        var response = InvokeProcessRequest(unknownRequest);

        // Assert
        Assert.IsType<ErrorResponse>(response);
        var errorResponse = (ErrorResponse)response;
        Assert.False(errorResponse.Ok);
        Assert.Contains("Unknown request type", errorResponse.Error);
    }

    private IpcResponse InvokeProcessRequest(IpcRequest request)
    {
        var pipeServerType = typeof(PipeServer);
        var method = pipeServerType.GetMethod("ProcessRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        using var fileWatcher = new PolicyFileWatcher(
            NullLogger<PolicyFileWatcher>.Instance,
            _mockEngine);

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            "1.0.0",
            _mockEngine,
            fileWatcher,
            _mockAuditLog);

        try
        {
            return (IpcResponse)method!.Invoke(pipeServer, new object[] { request })!;
        }
        finally
        {
            pipeServer.Dispose();
        }
    }
}

/// <summary>
/// Test helper: Unknown request type for testing error handling.
/// </summary>
public class UnknownTestRequest : IpcRequest
{
    public override string Type => "unknown-test-type";
}
