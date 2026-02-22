using BenchmarkDotNet.Attributes;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Benchmarks;

[MemoryDiagnoser]
public class PolicyValidatorBenchmarks
{
    private string _validPolicy10Rules = null!;
    private string _validPolicy50Rules = null!;
    private string _allInvalidRules = null!;

    [GlobalSetup]
    public void Setup()
    {
        _validPolicy10Rules = CreateValidPolicyJson(10);
        _validPolicy50Rules = CreateValidPolicyJson(50);
        _allInvalidRules = CreateAllInvalidRulesJson(50);
    }

    [Benchmark]
    public ValidationResult Validate_ValidPolicy_10Rules() =>
        PolicyValidator.ValidateJson(_validPolicy10Rules);

    [Benchmark]
    public ValidationResult Validate_PolicyWith50Rules() =>
        PolicyValidator.ValidateJson(_validPolicy50Rules);

    [Benchmark]
    public ValidationResult Validate_AllRulesInvalid() =>
        PolicyValidator.ValidateJson(_allInvalidRules);

    private static string CreateValidPolicyJson(int ruleCount)
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        for (int i = 0; i < ruleCount; i++)
        {
            policy.Rules.Add(new Rule
            {
                Id = $"bench-rule-{i}",
                Action = i % 2 == 0 ? "block" : "allow",
                Direction = "outbound",
                Protocol = "tcp",
                Remote = new EndpointFilter
                {
                    Ip = $"10.0.{i / 256}.{i % 256}",
                    Ports = $"{1000 + i}"
                },
                Priority = i,
                Enabled = true
            });
        }

        return policy.ToJson();
    }

    private static string CreateAllInvalidRulesJson(int ruleCount)
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        for (int i = 0; i < ruleCount; i++)
        {
            policy.Rules.Add(new Rule
            {
                Id = $"invalid rule {i}!", // invalid chars: spaces and !
                Action = "invalid-action",
                Direction = "sideways",
                Protocol = "smoke-signal",
                Priority = i,
                Enabled = true
            });
        }

        return policy.ToJson();
    }
}
