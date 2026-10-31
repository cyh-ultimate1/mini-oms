using MiniOMS.Domain;

namespace MiniOMS.Core;

public class Order
{
    public Guid ClOrdID { get; }
    public string Symbol { get; }
    public Side Side { get; }
    public OrderType OrderType { get; }
    public int Quantity { get; }
    public decimal? LimitPrice { get; }
    public OrderStatus Status { get; private set; }
    public int FilledQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public string? Reason { get; private set; }
    /// <summary>
    /// true when the order is finished for good
    /// </summary>
    public bool IsTerminal => Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected;
    private readonly HashSet<Guid> _seenExecIds = [];

    public Order(Guid clOrdID, string symbol, Side side, OrderType orderType, int quantity, decimal? limitPrice)
    {
        ClOrdID = clOrdID;
        Symbol = symbol;
        Side = side;
        OrderType = orderType;
        Quantity = quantity;
        LimitPrice = limitPrice;
        Status = OrderStatus.New;
    }
    /// <summary>
    /// "is this status change allowed?"
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns></returns>
    public static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        return from switch
        {
            OrderStatus.New => to is OrderStatus.Acknowledged or OrderStatus.Rejected,
            OrderStatus.Acknowledged => to is OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.Cancelled,
            OrderStatus.PartiallyFilled => to is OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.Cancelled,
            _ => false,
        };
    }

    public bool Apply(ExecutionReport report)
    {
        if (IsTerminal || _seenExecIds.Contains(report.ExecID) || !IsValidTransition(Status, report.Status))
            return false;

        _seenExecIds.Add(report.ExecID);
        if (report.LastQty > 0)
        {
            // rebuild "money so far" from the average and the quantity
            AvgFillPrice = (AvgFillPrice * FilledQuantity + report.LastPrice * report.LastQty) / (FilledQuantity + report.LastQty);
            FilledQuantity += report.LastQty;
        }
        Status = report.Status;
        Reason = report.Reason;
        return true;
    }

    public bool Reject(string reason)
    {
        if (Status != OrderStatus.New)
            return false;

        Status = OrderStatus.Rejected;
        Reason = reason;
        return true;
    }
}
