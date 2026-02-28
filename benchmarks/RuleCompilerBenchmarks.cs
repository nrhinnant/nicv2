using BenchmarkDotNet.Attributes;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Benchmarks;

[MemoryDiagnoser]
public class RuleCompilerBenchmarks
{
    private Policy _policy1Rule = null!;
    private Policy _policy10Rules = null!;
    private Policy _policy100Rules = null!;
    private Policy _policy500Rules = null!;
    private Policy _policyCidr = null!;
    private Policy _policyPortRange = null!;
    private Policy _policyMultiPort100 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _policy1Rule = CreatePolicy(1);
        _policy10Rules = CreatePolicy(10);
        _policy100Rules = CreatePolicy(100);
        _policy500Rules = CreatePolicy(500);
        _policyCidr = CreateCidrPolicy();
        _policyPortRange = CreatePortRangePolicy();
        _policyMultiPort100 = CreateMultiPortPolicy(100);
    }

    [Benchmark]
    public CompilationResult Compile1Rule() => RuleCompiler.Compile(_policy1Rule);

    [Benchmark]
    public CompilationResult Compile10Rules() => RuleCompiler.Compile(_policy10Rules);

    [Benchmark]
    public CompilationResult Compile100Rules() => RuleCompiler.Compile(_policy100Rules);

    [Benchmark]
    public CompilationResult Compile500Rules() => RuleCompiler.Compile(_policy500Rules);

    [Benchmark]
    public CompilationResult CompileWithCidr() => RuleCompiler.Compile(_policyCidr);

    [Benchmark]
    public CompilationResult CompileWithPortRange() => RuleCompiler.Compile(_policyPortRange);

    [Benchmark]
    public CompilationResult CompileMultiPort100() => RuleCompiler.Compile(_policyMultiPort100);

    private static Policy CreatePolicy(int ruleCount)
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

        return policy;
    }

    private static Policy CreateCidrPolicy()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        string[] cidrs =
        {
            "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
            "10.1.0.0/16", "10.2.0.0/16", "192.168.1.0/24",
            "192.168.2.0/24", "10.10.0.0/16", "172.16.1.0/24",
            "10.0.0.0/24"
        };

        for (int i = 0; i < cidrs.Length; i++)
        {
            policy.Rules.Add(new Rule
            {
                Id = $"cidr-rule-{i}",
                Action = "block",
                Direction = "outbound",
                Protocol = "tcp",
                Remote = new EndpointFilter { Ip = cidrs[i], Ports = "443" },
                Priority = i,
                Enabled = true
            });
        }

        return policy;
    }

    private static Policy CreatePortRangePolicy()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        for (int i = 0; i < 10; i++)
        {
            int start = 1024 + i * 6000;
            int end = Math.Min(start + 5999, 65535);
            policy.Rules.Add(new Rule
            {
                Id = $"portrange-rule-{i}",
                Action = "block",
                Direction = "outbound",
                Protocol = "tcp",
                Remote = new EndpointFilter
                {
                    Ip = $"10.0.{i}.0",
                    Ports = $"{start}-{end}"
                },
                Priority = i,
                Enabled = true
            });
        }

        return policy;
    }

    private static Policy CreateMultiPortPolicy(int portCount)
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        var ports = string.Join(",", Enumerable.Range(1000, portCount));
        policy.Rules.Add(new Rule
        {
            Id = "multiport-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "10.0.0.1", Ports = ports },
            Priority = 1,
            Enabled = true
        });

        return policy;
    }
}
