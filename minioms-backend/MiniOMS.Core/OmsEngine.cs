using MiniOMS.Domain;
using System.Collections.ObjectModel;

namespace MiniOMS.Core;

/// <summary>
/// manages each order's lifecycle.
/// it takes commands, runs risk checks, sends orders to the exchange through the gateway, and turns the exchange's reports into updated orders, positions and events.
/// It ties together Order, RiskChecker, Position and the gateway.
/// PlaceOrder ──► risk check ──► gateway ──► ExecutionReport ──► update Order + Position ──► events
/// </summary>
public class OmsEngine
{
    public OmsEngine(IExchangeGateway gateway, RiskChecker risk, TimeProvider time)
    {
        _gateway = gateway;
        _risk = risk;
        _time = time;
        _gateway.ExecutionReceived += OnExecutionReport;

        foreach (var symbolInfo in Symbols.All)
        {
            _positions.Add(symbolInfo.Symbol, new Position(symbolInfo.Symbol));
        }
    }

    readonly IExchangeGateway _gateway;
    readonly RiskChecker _risk;
    readonly TimeProvider _time;
    readonly Dictionary<Guid, Order> _orders = [];
    readonly Dictionary<string, Position> _positions = [];
    long _sequence;
    List<OmsEvent> _events = [];

    /// <summary>
    /// runs whenever the exchange sends a report. 
    /// </summary>
    /// <param name="report"></param>
    private void OnExecutionReport(ExecutionReport report)
    {
        if(!_orders.TryGetValue(report.ClOrdID, out var order))
        {
            return;
        }

        if (!order.Apply(report))
        {
            //an invalid transition or a duplicate fill
            return;
        }

        Publish(ToOrderUpdated(order));
        if(report.LastQty > 0)
        {
            if(_positions.TryGetValue(order.Symbol, out var position))
            {
                position.ApplyFill(order.Side, report.LastQty, report.LastPrice);
                Publish(new PositionUpdated(position.Symbol, position.Quantity, position.AvgPrice, position.RealizedPnl));
            }
        }
    }

    /// <summary>
    /// The single entry point for all commands. It sends each command to the right handler
    /// and returns every event it caused, in order.
    /// </summary>
    public IReadOnlyList<OmsEvent> Handle(OmsCommand command)
    {
        _events = [];
        switch (command)
        {
            case PlaceOrder poCmd:
                HandlePlace(poCmd);
                break;
            case CancelOrder coCmd:
                HandleCancel(coCmd);
                break;
        }
        return _events;
    }

    /// <summary>
    /// Creates a new order, runs pre-trade risk checks, and sends it to the exchange only if the checks pass.
    /// If they fail, it rejects the order and raises a risk alert. A duplicate ClOrdID is rejected immediately.
    /// </summary>
    private void HandlePlace(PlaceOrder cmd)
    {
        if (_orders.ContainsKey(cmd.ClOrdID))
        {
            Publish(new CommandRejected(cmd.CommandId, cmd.ClOrdID, "Duplicate ClOrdID"));
            return;
        }
        var newOrder = new Order(cmd.ClOrdID, cmd.Symbol, cmd.Side, cmd.OrderType, cmd.Quantity, cmd.LimitPrice);
        _orders.Add(cmd.ClOrdID, newOrder);
        Publish(ToOrderUpdated(newOrder));

        int positionQty = 0;
        if(_positions.TryGetValue(cmd.Symbol, out var position))
        {
            positionQty = position.Quantity;
        }
        var riskResult = _risk.Check(newOrder, positionQty);
        if (riskResult.Passed)
        {
            _gateway.SendNewOrder(new NewOrderRequest(cmd.ClOrdID, cmd.Symbol, cmd.Side, cmd.OrderType, cmd.Quantity, cmd.LimitPrice));
            return;
        }
        else
        {
            newOrder.Reject(riskResult.Message!);
            Publish(ToOrderUpdated(newOrder));
            Publish(new RiskAlert(newOrder.ClOrdID, riskResult.Rule!, riskResult.Message!));
            return;
        }
    }

    /// <summary>
    /// Sends a cancel request to the exchange for a live order.
    /// If the order is unknown or already finished (terminal), the command is rejected instead.
    /// </summary>
    private void HandleCancel(CancelOrder cmd)
    {
        if (!_orders.TryGetValue(cmd.ClOrdID, out var order))
        {
            Publish(new CommandRejected(cmd.CommandId, cmd.ClOrdID, "Unknown ClOrdID"));
            return;
        }

        if (order.IsTerminal)
        {
            Publish(new CommandRejected(cmd.CommandId, cmd.ClOrdID, $"Order is already {order.Status}"));
            return;
        }

        _gateway.SendCancel(new CancelRequest(order.ClOrdID, order.Symbol));
    }
    /// <summary>
    /// stamps an event with the next sequence number and the time, then saves it.
    /// </summary>
    /// <param name="evt"></param>
    private void Publish(OmsEvent evt)
    {
        _sequence += 1;
        var newEvent = evt with { Sequence = _sequence, TimestampUtc = _time.GetUtcNow().UtcDateTime };
        _events.Add(newEvent);
    }
    /// <summary>
    /// turns an Order into an OrderUpdated event.
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    private static OrderUpdated ToOrderUpdated(Order o)
    {
        return new OrderUpdated(o.ClOrdID,
                                o.Symbol,
                                o.Side,
                                o.OrderType,
                                o.Quantity,
                                o.LimitPrice,
                                o.Status,
                                o.FilledQuantity,
                                o.AvgFillPrice,
                                o.Reason);
    }
}
