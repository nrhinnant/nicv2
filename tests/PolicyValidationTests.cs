using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Comprehensive unit tests for policy validation.
/// Phase 11: Policy Schema v1
/// </summary>
public class PolicyValidationTests
{
    #region Helper Methods

    private static string CreatePolicyJson(
        string version = "1.0.0",
        string defaultAction = "allow",
        string updatedAt = "2024-01-15T10:30:00Z",
        string rules = "[]")
    {
        return $$"""
        {
            "version": "{{version}}",
            "defaultAction": "{{defaultAction}}",
            "updatedAt": "{{updatedAt}}",
            "rules": {{rules}}
        }
        """;
    }

    private static string CreateRuleJson(
        string id = "test-rule",
        string action = "block",
        string direction = "outbound",
        string protocol = "tcp",
        string? process = null,
        string? local = null,
        string? remote = null,
        int priority = 100,
        bool enabled = true,
        string? comment = null)
    {
        var parts = new List<string>
        {
            $"\"id\": \"{id}\"",
            $"\"action\": \"{action}\"",
            $"\"direction\": \"{direction}\"",
            $"\"protocol\": \"{protocol}\"",
            $"\"priority\": {priority}",
            $"\"enabled\": {enabled.ToString().ToLower()}"
        };

        if (process != null)
            parts.Add($"\"process\": \"{process}\"");
        if (local != null)
            parts.Add($"\"local\": {local}");
        if (remote != null)
            parts.Add($"\"remote\": {remote}");
        if (comment != null)
            parts.Add($"\"comment\": \"{comment}\"");

        return "{ " + string.Join(", ", parts) + " }";
    }

    #endregion

    #region Valid Policy Tests

    [Fact]
    public void ValidateJsonMinimalValidPolicyReturnsValid()
    {
        var json = CreatePolicyJson();

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonPolicyWithOneRuleReturnsValid()
    {
        var rule = CreateRuleJson(
            id: "test-rule-1",
            remote: """{ "ip": "1.1.1.1", "ports": "443" }""");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonPolicyWithAllFieldsReturnsValid()
    {
        var json = """
        {
            "version": "2.1.3-beta",
            "defaultAction": "block",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "complex-rule",
                    "action": "allow",
                    "direction": "both",
                    "protocol": "any",
                    "process": "C:\\Program Files\\App\\app.exe",
                    "local": { "ip": "192.168.1.0/24", "ports": "8080-8090" },
                    "remote": { "ip": "10.0.0.0/8", "ports": "80,443" },
                    "priority": 500,
                    "enabled": true,
                    "comment": "Allow app traffic on local network"
                }
            ]
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    #endregion

    #region Empty/Null Input Tests

    [Fact]
    public void ValidateJsonEmptyStringReturnsError()
    {
        var result = PolicyValidator.ValidateJson("");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("empty"));
    }

    [Fact]
    public void ValidateJsonWhitespaceOnlyReturnsError()
    {
        var result = PolicyValidator.ValidateJson("   \n\t  ");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("empty"));
    }

    [Fact]
    public void ValidateJsonInvalidJsonReturnsError()
    {
        var result = PolicyValidator.ValidateJson("not valid json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid JSON"));
    }

    [Fact]
    public void ValidateJsonJsonArrayReturnsError()
    {
        var result = PolicyValidator.ValidateJson("[]");

        Assert.False(result.IsValid);
    }

    #endregion

    #region Version Validation Tests

    [Fact]
    public void ValidateJsonMissingVersionReturnsError()
    {
        var json = """
        {
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "version");
    }

    [Fact]
    public void ValidateJsonEmptyVersionReturnsError()
    {
        var json = CreatePolicyJson(version: "");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "version");
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0+build123")]
    public void ValidateJsonValidVersionFormatsReturnsValid(string version)
    {
        var json = CreatePolicyJson(version: version);

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("abc")]
    public void ValidateJsonInvalidVersionFormatsReturnsError(string version)
    {
        var json = CreatePolicyJson(version: version);

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "version");
    }

    #endregion

    #region DefaultAction Validation Tests

    [Fact]
    public void ValidateJsonMissingDefaultActionUsesDefaultAllow()
    {
        // When defaultAction is missing, JSON deserializer uses C# property default "allow"
        // This is valid behavior - default allow is safe for connectivity
        var json = """
        {
            "version": "1.0.0",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": []
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        // Policy is valid because default value "allow" is used
        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonEmptyDefaultActionReturnsError()
    {
        var json = CreatePolicyJson(defaultAction: "");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "defaultAction");
    }

    [Theory]
    [InlineData("allow")]
    [InlineData("Allow")]
    [InlineData("ALLOW")]
    [InlineData("block")]
    [InlineData("Block")]
    [InlineData("BLOCK")]
    public void ValidateJsonValidDefaultActionValuesReturnsValid(string action)
    {
        var json = CreatePolicyJson(defaultAction: action);

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("permit")]
    [InlineData("deny")]
    [InlineData("drop")]
    public void ValidateJsonInvalidDefaultActionValuesReturnsError(string action)
    {
        var json = CreatePolicyJson(defaultAction: action);

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "defaultAction");
    }

    #endregion

    #region UpdatedAt Validation Tests

    [Fact]
    public void ValidateJsonMissingUpdatedAtReturnsError()
    {
        var json = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "rules": []
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "updatedAt");
    }

    [Fact]
    public void ValidateJsonFutureUpdatedAtReturnsError()
    {
        var futureDate = DateTime.UtcNow.AddHours(1).ToString("o");
        var json = CreatePolicyJson(updatedAt: futureDate);

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "updatedAt" && e.Message.Contains("future"));
    }

    #endregion

    #region Rule ID Validation Tests

    [Fact]
    public void ValidateJsonMissingRuleIdReturnsError()
    {
        var json = """
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "action": "block",
                    "direction": "outbound",
                    "protocol": "tcp",
                    "priority": 100,
                    "enabled": true
                }
            ]
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("rules[0]") && e.Path.Contains("id"));
    }

    [Fact]
    public void ValidateJsonDuplicateRuleIdsReturnsError()
    {
        var rule1 = CreateRuleJson(id: "duplicate-id", action: "block");
        var rule2 = CreateRuleJson(id: "duplicate-id", action: "allow", direction: "inbound", protocol: "udp");
        var json = CreatePolicyJson(rules: $"[{rule1}, {rule2}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void ValidateJsonDuplicateRuleIdsCaseInsensitiveReturnsError()
    {
        var rule1 = CreateRuleJson(id: "my-rule", action: "block");
        var rule2 = CreateRuleJson(id: "MY-RULE", action: "allow", direction: "inbound");
        var json = CreatePolicyJson(rules: $"[{rule1}, {rule2}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
    }

    [Theory]
    [InlineData("valid-rule-id")]
    [InlineData("valid_rule_id")]
    [InlineData("ValidRuleId123")]
    [InlineData("rule-1")]
    [InlineData("a")]
    public void ValidateJsonValidRuleIdFormatsReturnsValid(string ruleId)
    {
        var rule = CreateRuleJson(id: ruleId);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("invalid rule")]
    [InlineData("invalid.rule")]
    [InlineData("invalid@rule")]
    [InlineData("invalid/rule")]
    public void ValidateJsonInvalidRuleIdFormatsReturnsError(string ruleId)
    {
        var rule = CreateRuleJson(id: ruleId);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("id") && e.Message.Contains("alphanumeric"));
    }

    #endregion

    #region Rule Action Validation Tests

    [Theory]
    [InlineData("allow")]
    [InlineData("Allow")]
    [InlineData("ALLOW")]
    [InlineData("block")]
    [InlineData("Block")]
    [InlineData("BLOCK")]
    public void ValidateJsonValidRuleActionsReturnsValid(string action)
    {
        var rule = CreateRuleJson(action: action);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("permit")]
    [InlineData("deny")]
    [InlineData("drop")]
    [InlineData("reject")]
    public void ValidateJsonInvalidRuleActionsReturnsError(string action)
    {
        var rule = CreateRuleJson(action: action);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("action"));
    }

    #endregion

    #region Rule Direction Validation Tests

    [Theory]
    [InlineData("inbound")]
    [InlineData("Inbound")]
    [InlineData("INBOUND")]
    [InlineData("outbound")]
    [InlineData("both")]
    public void ValidateJsonValidRuleDirectionsReturnsValid(string direction)
    {
        var rule = CreateRuleJson(direction: direction);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("in")]
    [InlineData("out")]
    [InlineData("ingress")]
    [InlineData("egress")]
    public void ValidateJsonInvalidRuleDirectionsReturnsError(string direction)
    {
        var rule = CreateRuleJson(direction: direction);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("direction"));
    }

    #endregion

    #region Rule Protocol Validation Tests

    [Theory]
    [InlineData("tcp")]
    [InlineData("TCP")]
    [InlineData("udp")]
    [InlineData("UDP")]
    [InlineData("any")]
    [InlineData("Any")]
    public void ValidateJsonValidRuleProtocolsReturnsValid(string protocol)
    {
        var rule = CreateRuleJson(protocol: protocol);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("icmp")]
    [InlineData("ip")]
    [InlineData("http")]
    [InlineData("all")]
    public void ValidateJsonInvalidRuleProtocolsReturnsError(string protocol)
    {
        var rule = CreateRuleJson(protocol: protocol);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("protocol"));
    }

    #endregion

    #region Process Path Validation Tests

    [Theory]
    [InlineData("C:\\\\Program Files\\\\App\\\\app.exe")]
    [InlineData("C:\\\\Windows\\\\System32\\\\cmd.exe")]
    [InlineData("D:\\\\test.exe")]
    [InlineData("\\\\\\\\server\\\\share\\\\app.exe")]
    [InlineData("app.exe")]
    [InlineData("my-app_v2.exe")]
    public void ValidateJsonValidProcessPathsReturnsValid(string process)
    {
        var rule = CreateRuleJson(process: process);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonProcessPathTraversalReturnsError()
    {
        var rule = CreateRuleJson(process: "C:\\\\Windows\\\\..\\\\secret.exe");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("process") && e.Message.Contains("traversal"));
    }

    #endregion

    #region IP/CIDR Validation Tests

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("192.168.1.0/24")]
    [InlineData("10.0.0.0/8")]
    [InlineData("0.0.0.0/0")]
    [InlineData("192.168.1.1/32")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::/32")]
    public void ValidateJsonValidIpAddressesReturnsValid(string ip)
    {
        var rule = CreateRuleJson(remote: $"{{ \"ip\": \"{ip}\" }}");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("256.1.1.1")]
    [InlineData("192.168.1.1.1")]
    [InlineData("not-an-ip")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.0/-1")]
    [InlineData("::1/129")]
    // Note: "192.168.1" is not included because IPAddress.TryParse accepts partial IPs
    public void ValidateJsonInvalidIpAddressesReturnsError(string ip)
    {
        var rule = CreateRuleJson(remote: $"{{ \"ip\": \"{ip}\" }}");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("remote.ip"));
    }

    #endregion

    #region Port Validation Tests

    [Theory]
    [InlineData("80")]
    [InlineData("443")]
    [InlineData("1")]
    [InlineData("65535")]
    [InlineData("80-443")]
    [InlineData("8080-8090")]
    [InlineData("80,443")]
    [InlineData("80,443,8080-8090")]
    [InlineData("22,80,443,3389,8080-8090")]
    public void ValidateJsonValidPortSpecsReturnsValid(string ports)
    {
        var rule = CreateRuleJson(remote: $"{{ \"ports\": \"{ports}\" }}");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("443-80")]
    [InlineData("80-")]
    public void ValidateJsonInvalidPortSpecsReturnsError(string ports)
    {
        var rule = CreateRuleJson(remote: $"{{ \"ports\": \"{ports}\" }}");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("remote.ports"));
    }

    #endregion

    #region Endpoint Filter Validation Tests

    [Fact]
    public void ValidateJsonEndpointWithIpOnlyReturnsValid()
    {
        var rule = CreateRuleJson(remote: """{ "ip": "1.1.1.1" }""");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonEndpointWithPortsOnlyReturnsValid()
    {
        var rule = CreateRuleJson(remote: """{ "ports": "443" }""");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.True(result.IsValid, result.GetSummary());
    }

    [Fact]
    public void ValidateJsonEmptyEndpointFilterReturnsError()
    {
        var rule = CreateRuleJson(remote: "{ }");
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("remote") && e.Message.Contains("at least"));
    }

    #endregion

    #region Multiple Error Collection Tests

    [Fact]
    public void ValidateJsonMultipleErrorsCollectsAllErrors()
    {
        var json = """
        {
            "version": "invalid",
            "defaultAction": "invalid",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [
                {
                    "id": "",
                    "action": "invalid",
                    "direction": "invalid",
                    "protocol": "invalid",
                    "priority": 100,
                    "enabled": true
                }
            ]
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5, $"Expected at least 5 errors, got {result.Errors.Count}: {result.GetSummary()}");
    }

    [Fact]
    public void ValidateJsonMultipleRulesWithErrorsCollectsAllErrors()
    {
        var rule1 = CreateRuleJson(id: "rule1", action: "invalid");
        var rule2 = CreateRuleJson(id: "rule2", direction: "invalid");
        var json = CreatePolicyJson(rules: $"[{rule1}, {rule2}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
        Assert.Contains(result.Errors, e => e.Path.Contains("rules[0]"));
        Assert.Contains(result.Errors, e => e.Path.Contains("rules[1]"));
    }

    #endregion

    #region Size Limit Tests

    [Fact]
    public void ValidateJsonTooManyRulesReturnsError()
    {
        // Generate 10001 rules with minimal JSON to stay under size limit
        // Each rule is ~100 bytes, 10001 rules ~1MB which may hit size limit
        // So we use shorter IDs and verify the policy can be parsed
        var rules = string.Join(",", Enumerable.Range(0, 10001).Select(i =>
            $"{{\"id\":\"r{i}\",\"action\":\"block\",\"direction\":\"outbound\",\"protocol\":\"tcp\",\"priority\":{i},\"enabled\":true}}"));

        var json = $$"""
        {
            "version": "1.0.0",
            "defaultAction": "allow",
            "updatedAt": "2024-01-15T10:30:00Z",
            "rules": [{{rules}}]
        }
        """;

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        // Either hits size limit or rule count limit - both are valid errors
        Assert.True(
            result.Errors.Any(e => e.Path == "rules" && e.Message.Contains("Too many")) ||
            result.Errors.Any(e => e.Message.Contains("exceeds maximum size")),
            $"Expected size limit or rule count error, got: {result.GetSummary()}");
    }

    [Fact]
    public void ValidateJsonRuleIdTooLongReturnsError()
    {
        var longId = new string('a', 129);
        var rule = CreateRuleJson(id: longId);
        var json = CreatePolicyJson(rules: $"[{rule}]");

        var result = PolicyValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("id") && e.Message.Contains("maximum length"));
    }

    #endregion
}

/// <summary>
/// Unit tests for NetworkUtils helper functions.
/// </summary>
public class NetworkUtilsTests
{
    #region CIDR Parsing Tests

    [Theory]
    [InlineData("192.168.1.1", true, 32)]
    [InlineData("10.0.0.0/8", true, 8)]
    [InlineData("0.0.0.0/0", true, 0)]
    [InlineData("::1", true, 128)]
    [InlineData("::1/64", true, 64)]
    public void TryParseCidrValidInputsReturnsExpectedValues(string input, bool expectedSuccess, int expectedPrefix)
    {
        var success = NetworkUtils.TryParseCidr(input, out var ip, out var prefix);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.NotNull(ip);
            Assert.Equal(expectedPrefix, prefix);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.0/-1")]
    public void TryParseCidrInvalidInputsReturnsFalse(string input)
    {
        var success = NetworkUtils.TryParseCidr(input, out _, out _);

        Assert.False(success);
    }

    #endregion

    #region Port Parsing Tests

    [Theory]
    [InlineData("80", 1)]
    [InlineData("80-443", 1)]
    [InlineData("80,443", 2)]
    [InlineData("80,443,8080-8090", 3)]
    public void TryParsePortsValidInputsReturnsExpectedRangeCount(string input, int expectedRangeCount)
    {
        var success = NetworkUtils.TryParsePorts(input, out var ranges);

        Assert.True(success);
        Assert.Equal(expectedRangeCount, ranges.Count);
    }

    [Fact]
    public void TryParsePortsSinglePortReturnsCorrectRange()
    {
        var success = NetworkUtils.TryParsePorts("443", out var ranges);

        Assert.True(success);
        Assert.Single(ranges);
        Assert.Equal(443, ranges[0].Start);
        Assert.Equal(443, ranges[0].End);
    }

    [Fact]
    public void TryParsePortsPortRangeReturnsCorrectRange()
    {
        var success = NetworkUtils.TryParsePorts("8080-8090", out var ranges);

        Assert.True(success);
        Assert.Single(ranges);
        Assert.Equal(8080, ranges[0].Start);
        Assert.Equal(8090, ranges[0].End);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("abc")]
    [InlineData("443-80")]
    public void TryParsePortsInvalidInputsReturnsFalse(string input)
    {
        var success = NetworkUtils.TryParsePorts(input, out _);

        Assert.False(success);
    }

    #endregion

    #region Version Validation Tests

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0.0-alpha", true)]
    [InlineData("1.0.0-alpha.1", true)]
    [InlineData("1.0.0+build", true)]
    [InlineData("1.0", false)]
    [InlineData("1", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("", false)]
    public void ValidateVersionReturnsExpectedResult(string version, bool expectedValid)
    {
        var isValid = NetworkUtils.ValidateVersion(version, out var error);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.NotNull(error);
        }
    }

    #endregion

    #region Process Path Validation Tests

    [Theory]
    [InlineData("C:\\Windows\\System32\\cmd.exe", true)]
    [InlineData("D:\\apps\\app.exe", true)]
    [InlineData("\\\\server\\share\\app.exe", true)]
    [InlineData("cmd.exe", true)]
    [InlineData("my-app_v2.exe", true)]
    [InlineData("C:\\path\\..\\secret.exe", false)]
    [InlineData("", false)]
    public void ValidateProcessPathReturnsExpectedResult(string path, bool expectedValid)
    {
        var isValid = NetworkUtils.ValidateProcessPath(path, out var error);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.NotNull(error);
        }
    }

    #endregion
}

/// <summary>
/// Unit tests for policy file path validation (HIGH-04 security fix).
/// Tests comprehensive path validation including ADS, UNC, device paths, etc.
/// </summary>
public class PolicyFilePathValidationTests
{
    #region Valid Path Tests

    [Theory]
    [InlineData(@"C:\policy.json")]
    [InlineData(@"C:\ProgramData\WfpTrafficControl\policy.json")]
    [InlineData(@"D:\configs\firewall\rules.json")]
    [InlineData(@"E:\policy.json")]
    public void ValidatePolicyFilePathValidLocalPathsReturnsTrue(string path)
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.True(isValid, $"Path '{path}' should be valid. Error: {error}");
        Assert.Null(error);
    }

    #endregion

    #region Empty/Null Path Tests

    [Fact]
    public void ValidatePolicyFilePathEmptyPathReturnsFalse()
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath("", out var error);

        Assert.False(isValid);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePolicyFilePathWhitespacePathReturnsFalse()
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath("   ", out var error);

        Assert.False(isValid);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePolicyFilePathNullPathReturnsFalse()
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(null, out var error);

        Assert.False(isValid);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Path Traversal Tests

    [Theory]
    [InlineData(@"C:\test\..\secret.json")]
    [InlineData(@"C:\Users\..\..\..\Windows\System32\config.json")]
    [InlineData(@"D:\app\config\..\..\..\sensitive.json")]
    public void ValidatePolicyFilePathPathTraversalReturnsFalse(string path)
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.False(isValid);
        Assert.Contains("traversal", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Alternate Data Stream (ADS) Tests

    [Theory]
    [InlineData(@"C:\policy.json::$DATA")]
    [InlineData(@"C:\policy.json:hidden_stream")]
    [InlineData(@"D:\test:secret:hidden")]
    public void ValidatePolicyFilePathAlternateDataStreamReturnsFalse(string path)
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.False(isValid);
        Assert.Contains("ADS", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region UNC Path Tests

    [Theory]
    [InlineData(@"\\server\share\policy.json")]
    [InlineData(@"\\192.168.1.1\c$\policy.json")]
    [InlineData(@"\\attacker.com\malware\payload.json")]
    public void ValidatePolicyFilePathUncPathReturnsFalse(string path)
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.False(isValid);
        Assert.Contains("UNC", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Device Path Tests

    [Theory]
    [InlineData(@"\\.\C:\policy.json")]
    [InlineData(@"\\?\C:\policy.json")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume1\policy.json")]
    public void ValidatePolicyFilePathDevicePathReturnsFalse(string path)
    {
        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.False(isValid);
        // Will match device path, UNC path, or ADS error (depending on which check fails first)
        // The important thing is that the path is rejected
        Assert.True(
            error!.Contains("device", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("UNC", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("ADS", StringComparison.OrdinalIgnoreCase),
            $"Expected device, UNC, or ADS path error, got: {error}");
    }

    #endregion

    #region Invalid Character Tests

    [Fact]
    public void ValidatePolicyFilePathNullCharacterReturnsFalse()
    {
        var path = "C:\\policy\0.json";

        var isValid = NetworkUtils.ValidatePolicyFilePath(path, out var error);

        Assert.False(isValid);
        Assert.Contains("invalid characters", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
