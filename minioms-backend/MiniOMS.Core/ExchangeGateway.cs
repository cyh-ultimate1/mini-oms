using MiniOMS.Domain;
using MiniOMS.Exchange;

namespace MiniOMS.Core;

/// <summary>
/// the OMS talks to the exchange only through an interface. 
/// Later, a real FIX connection could replace the in-process exchange without changing the OMS
/// </summary>
public interface IExchangeGateway
{
    event Action<ExecutionReport>? ExecutionReceived;
    void SendNewOrder(NewOrderRequest request);
    void SendCancel(CancelRequest request);
}

/// <summary>
/// An exchange that runs inside the same process. It holds one order book per symbol and sends every
/// execution report back through the ExecutionReceived event. It's swappable for a real exchange
/// connection (for example FIX) through IExchangeGateway.
/// </summary>
public class InProcessExchangeGateway : IExchangeGateway
{
    public InProcessExchangeGateway()
    {
        foreach(var symbolInfo in Symbols.All)
        {
            _books.Add(symbolInfo.Symbol, new OrderBook(symbolInfo.Symbol));
        }
    }
    private readonly Dictionary<string, OrderBook> _books = [];
    public event Action<ExecutionReport>? ExecutionReceived;

    /// <summary>
    /// Sends a cancel to the order book for its symbol and passes on the resulting report.
    /// An unknown symbol is rejected.
    /// </summary>
    public void SendCancel(CancelRequest request)
    {
        if (!_books.TryGetValue(request.Symbol, out var orderBook))
        {
            ExecutionReceived?.Invoke(new ExecutionReport(ExecID: Guid.NewGuid(),
                                                          ClOrdID: request.ClOrdID,
                                                          Symbol: request.Symbol,
                                                          Status: OrderStatus.Rejected,
                                                          LastQty: 0,
                                                          LastPrice: 0,
                                                          FilledQuantity: 0,
                                                          LeavesQty: 0,
                                                          Reason: "Unknown symbol"));
            return;
        }
        var reports = orderBook.CancelOrder(request);
        foreach (var report in reports)
        {
            ExecutionReceived?.Invoke(report);
        }
    }

    /// <summary>
    /// Sends a new order to the order book for its symbol and passes on every report the book produces.
    /// An unknown symbol is rejected.
    /// </summary>
    public void SendNewOrder(NewOrderRequest request)
    {
        if (!_books.TryGetValue(request.Symbol, out var orderBook))
        {
            ExecutionReceived?.Invoke(new ExecutionReport(ExecID: Guid.NewGuid(),
                                                          ClOrdID: request.ClOrdID,
                                                          Symbol: request.Symbol,
                                                          Status: OrderStatus.Rejected,
                                                          LastQty: 0,
                                                          LastPrice: 0,
                                                          FilledQuantity: 0,
                                                          LeavesQty: 0,
                                                          Reason: "Unknown symbol"));
            return;
        }
        var reports = orderBook.AddOrder(request);
        foreach(var report in reports)
        {
            ExecutionReceived?.Invoke(report);
        }

    }

    /// <summary>
    /// Returns the order book for a symbol. Used by the liquidity seeder.
    /// </summary>
    public OrderBook GetBook(string symbol)
    {
        return _books[symbol];
    }
}
