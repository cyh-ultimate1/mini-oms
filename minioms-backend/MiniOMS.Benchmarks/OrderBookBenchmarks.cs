using BenchmarkDotNet.Attributes;
using MiniOMS.Domain;
using MiniOMS.Exchange;

namespace MiniOMS.Benchmarks;

[MemoryDiagnoser]
public class OrderBookBenchmarks
{
    private OrderBook _book = null!;
    private NewOrderRequest _restingBuy = null!;
    private CancelRequest _cancelBuy = null!;
    private NewOrderRequest _restingSell = null!;
    /// <summary>
    /// it will match _restingSell fully
    /// </summary>
    private NewOrderRequest _matchingBuy = null!;

    /// <summary>
    /// builds a realistic book once, before measuring.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var symbol = "ACME";
        _book = new OrderBook(symbol);
        for(int i = 0; i < 5; i++)
        {
            _book.AddOrder(new NewOrderRequest(Guid.NewGuid(), symbol, Side.Buy, OrderType.Limit, 100, 98.60M + (i * 0.1M)));
        }
        for (int i = 0; i < 5; i++)
        {
            _book.AddOrder(new NewOrderRequest(Guid.NewGuid(), symbol, Side.Sell, OrderType.Limit, 100, 102.00M + (i * 0.1M)));
        }
        _restingBuy = new NewOrderRequest(Guid.NewGuid(), symbol, Side.Buy, OrderType.Limit, 100, 100.00M);
        _restingSell = new NewOrderRequest(Guid.NewGuid(), symbol, Side.Sell, OrderType.Limit, 100, 100.00M);
        _cancelBuy = new CancelRequest(_restingBuy.ClOrdID, symbol);
        _matchingBuy = new NewOrderRequest(Guid.NewGuid(), symbol, Side.Buy, OrderType.Limit, 100, 100.00M);
    }

    /// <summary>
    /// the cost of resting an order and cancelling it.
    /// </summary>
    [Benchmark]
    public void AddAndCancel()
    {
        _book.AddOrder(_restingBuy);
        _book.CancelOrder(_cancelBuy);
    }
    /// <summary>
    /// the cost of a full match.
    /// </summary>
    [Benchmark]
    public void RestThenMatch()
    {
        _book.AddOrder(_restingSell);
        _book.AddOrder(_matchingBuy);
    }
}
