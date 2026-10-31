using BenchmarkDotNet.Attributes;
using MiniOMS.Core;
using MiniOMS.Domain;

namespace MiniOMS.Benchmarks;

[MemoryDiagnoser]
public class RiskBenchmarks
{
    private RiskChecker _risk = null!;
    private Order _order = null!;

    [GlobalSetup]
    public void Setup()
    {
        _risk = new RiskChecker(new RiskLimits());
        _order = new Order(Guid.NewGuid(), "ACME", Side.Buy, OrderType.Limit, 100, 100.00m);
    }

    [Benchmark] 
    public RiskResult Check() => _risk.Check(_order, 0);
}
