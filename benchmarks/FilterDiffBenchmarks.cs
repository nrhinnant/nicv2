using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Benchmarks;

[MemoryDiagnoser]
public class FilterDiffBenchmarks
{
    private List<CompiledFilter> _empty = null!;
    private List<ExistingFilter> _emptyExisting = null!;
    private List<CompiledFilter> _desired100 = null!;
    private List<ExistingFilter> _existing100Same = null!;
    private List<CompiledFilter> _desired100HalfChanged = null!;
    private List<ExistingFilter> _existing100ForHalfChanged = null!;
    private List<CompiledFilter> _desired500 = null!;
    private List<ExistingFilter> _existing500AllChanged = null!;

    [GlobalSetup]
    public void Setup()
    {
        _empty = new List<CompiledFilter>();
        _emptyExisting = new List<ExistingFilter>();

        // 100 desired filters
        _desired100 = CreateCompiledFilters(100, 0);

        // Same 100 as ExistingFilter (idempotent case)
        _existing100Same = _desired100.Select((f, i) => new ExistingFilter
        {
            FilterKey = f.FilterKey,
            FilterId = (ulong)i,
            DisplayName = f.DisplayName
        }).ToList();

        // Half-changed: desired has 50 same + 50 new, current has 50 same + 50 old
        var shared50 = CreateCompiledFilters(50, 0);
        var new50 = CreateCompiledFilters(50, 1000);
        _desired100HalfChanged = shared50.Concat(new50).ToList();

        var old50 = CreateCompiledFilters(50, 2000);
        var existingShared = shared50.Select((f, i) => new ExistingFilter
        {
            FilterKey = f.FilterKey,
            FilterId = (ulong)i,
            DisplayName = f.DisplayName
        });
        var existingOld = old50.Select((f, i) => new ExistingFilter
        {
            FilterKey = f.FilterKey,
            FilterId = (ulong)(i + 50),
            DisplayName = f.DisplayName
        });
        _existing100ForHalfChanged = existingShared.Concat(existingOld).ToList();

        // 500 all changed: completely disjoint sets
        _desired500 = CreateCompiledFilters(500, 0);
        var old500 = CreateCompiledFilters(500, 5000);
        _existing500AllChanged = old500.Select((f, i) => new ExistingFilter
        {
            FilterKey = f.FilterKey,
            FilterId = (ulong)i,
            DisplayName = f.DisplayName
        }).ToList();
    }

    [Benchmark]
    public FilterDiff DiffEmptyToEmpty() =>
        FilterDiffComputer.ComputeDiff(_empty, _emptyExisting);

    [Benchmark]
    public FilterDiff DiffEmptyTo100() =>
        FilterDiffComputer.ComputeDiff(_desired100, _emptyExisting);

    [Benchmark]
    public FilterDiff Diff100ToSame100() =>
        FilterDiffComputer.ComputeDiff(_desired100, _existing100Same);

    [Benchmark]
    public FilterDiff Diff100To100HalfChanged() =>
        FilterDiffComputer.ComputeDiff(_desired100HalfChanged, _existing100ForHalfChanged);

    [Benchmark]
    public FilterDiff Diff500To500AllChanged() =>
        FilterDiffComputer.ComputeDiff(_desired500, _existing500AllChanged);

    private static List<CompiledFilter> CreateCompiledFilters(int count, int offset)
    {
        var filters = new List<CompiledFilter>(count);
        for (int i = 0; i < count; i++)
        {
            var idx = i + offset;
            filters.Add(new CompiledFilter
            {
                FilterKey = GenerateDeterministicGuid(idx),
                DisplayName = $"WfpTrafficControl: bench-filter-{idx}",
                Description = $"Benchmark filter {idx}",
                Action = idx % 2 == 0 ? FilterAction.Block : FilterAction.Allow,
                Weight = 1000 + (ulong)idx,
                RuleId = $"bench-rule-{idx}",
                Protocol = 6,
                Direction = "outbound",
                RemoteIpAddress = (uint)(0x0A000000 + idx),
                RemoteIpMask = 0xFFFFFFFF,
                RemotePort = (ushort)(1000 + idx % 64000)
            });
        }
        return filters;
    }

    private static Guid GenerateDeterministicGuid(int index)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"bench-filter:{index}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
