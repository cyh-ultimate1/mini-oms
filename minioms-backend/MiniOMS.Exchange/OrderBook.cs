using MiniOMS.Domain;

namespace MiniOMS.Exchange;

/// <summary>
/// The order book for one symbol: waiting buy orders (bids) and sell orders (asks).
/// Matches incoming orders by price-time priority and returns execution reports.
/// </summary>
public class OrderBook
{
    public OrderBook(string symbol)
    {
        _symbol = symbol;
    }

    // Key -> price, Value -> List of RestingOrder
    private readonly SortedDictionary<decimal, LinkedList<RestingOrder>> _bids = new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
    private readonly SortedDictionary<decimal, LinkedList<RestingOrder>> _asks = [];
    private readonly Dictionary<Guid, LinkedListNode<RestingOrder>> _ordersById = [];
    private readonly string _symbol;
    /// <summary>Highest waiting buy price, or null if there are no bids.</summary>
    public decimal? BestBid => _bids.Count > 0 ? GetBestPrice(Side.Buy) : null;
    /// <summary>Lowest waiting sell price, or null if there are no asks.</summary>
    public decimal? BestAsk => _asks.Count > 0 ? GetBestPrice(Side.Sell) : null;
    // Reused for every call to avoid allocating a new list (hot path).
    private readonly List<ExecutionReport> _reports = new(8);
    // Empty price levels kept for reuse, so busy prices don't create garbage. Pop when a level is needed, push when one empties.
    private readonly Stack<LinkedList<RestingOrder>> _levelPool = [];

    /// <summary>
    /// Accepts a new order: acknowledges it, matches it against the opposite side,
    /// then rests any leftover (limit) or cancels it (market).
    /// The returned list is reused, so it's only valid until the next call.
    /// </summary>
    public IReadOnlyList<ExecutionReport> AddOrder(NewOrderRequest order)
    {
        _reports.Clear();
        // Market Order will accept any price, should not be stopped.
        var isOrderTypeLimit = order.OrderType == OrderType.Limit;
        if (isOrderTypeLimit && !order.LimitPrice.HasValue)
        {
            _reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                        ClOrdID: order.ClOrdID,
                                        Symbol: _symbol,
                                        Status: OrderStatus.Rejected,
                                        LastQty: 0,
                                        LastPrice: 0,
                                        FilledQuantity: 0,
                                        LeavesQty: 0,
                                        Reason: "Limit order needs a price"));
            return _reports;
        }

        // 1. Acknowledged
        _reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                    ClOrdID: order.ClOrdID,
                                    Symbol: _symbol,
                                    Status: OrderStatus.Acknowledged,
                                    LastQty: 0,
                                    LastPrice: 0,
                                    FilledQuantity: 0,
                                    LeavesQty: order.Quantity,
                                    Reason: null));
        // 2. Match against the opposite side
        int remainingQty = Match(order, _reports);

        // 3. Rest only what's left
        if (remainingQty > 0)
        {
            if (isOrderTypeLimit)
            {
                // LimitPrice will always have value since already pass the gate above.
                var limitPrice = order.LimitPrice!.Value;
                // 1. Add new node to the Linked list in _bids or _asks.
                // 2. Add the new node to the orders list.
                var newNode = new LinkedListNode<RestingOrder>(new RestingOrder(clOrdID: order.ClOrdID,
                                                                                side: order.Side,
                                                                                price: limitPrice,
                                                                                remainingQty: remainingQty,
                                                                                filledQty: order.Quantity - remainingQty));

                var sideList = order.Side == Side.Buy ? _bids : _asks;
                if (!sideList.TryGetValue(limitPrice, out var level))
                {
                    if(!_levelPool.TryPop(out level))
                    {
                        level = new LinkedList<RestingOrder>();
                    }
                    sideList[limitPrice] = level;
                }
                level.AddLast(newNode);
                _ordersById[order.ClOrdID] = newNode;
            }
            else
            {
                _reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                                         ClOrdID: order.ClOrdID,
                                                         Symbol: _symbol,
                                                         Status: OrderStatus.Cancelled,
                                                         LastQty: 0,
                                                         LastPrice: 0,
                                                         FilledQuantity: order.Quantity - remainingQty,
                                                         LeavesQty: 0,
                                                         Reason: "No more liquidity"));
            }
        }

        return _reports;
    }
    /// <summary>
    /// Removes a waiting order from the book. An unknown ClOrdID is rejected.
    /// The returned list is reused, so it's only valid until the next call.
    /// </summary>
    public IReadOnlyList<ExecutionReport> CancelOrder(CancelRequest request)
    {
        _reports.Clear();
        if (!_ordersById.TryGetValue(request.ClOrdID, out var restingOrderNode))
        {
            _reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                        ClOrdID: request.ClOrdID,
                                        Symbol: _symbol,
                                        Status: OrderStatus.Rejected,
                                        LastQty: 0,
                                        LastPrice: 0,
                                        FilledQuantity: 0,
                                        LeavesQty: 0,
                                        Reason: "Unknown Order"));
            return _reports;
        }
        var restingOrder = restingOrderNode.Value;
        // Remove from orders list since want to Cancel.
        _ordersById.Remove(restingOrder.ClOrdID);

        var sideList = restingOrder.Side == Side.Buy ? _bids : _asks;
        var priceLevelRestingOrders = restingOrderNode.List!;
        // Remove this node from the linked list, so that it will not be matched in the future.
        priceLevelRestingOrders.Remove(restingOrderNode);
        // if the price level has no more resting orders, should remove the price key value from the side list.
        // else, will disrupt the finding of the best bid/ask price.
        if (priceLevelRestingOrders.Count == 0)
        {
            sideList.Remove(restingOrder.Price);
            _levelPool.Push(priceLevelRestingOrders);
        }

        _reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                    ClOrdID: restingOrder.ClOrdID,
                                    Symbol: _symbol,
                                    Status: OrderStatus.Cancelled,
                                    LastQty: 0,
                                    LastPrice: 0,
                                    FilledQuantity: restingOrder.FilledQty,
                                    LeavesQty: 0,
                                    Reason: null));
        return _reports;
    }
    /// <summary>
    /// Can the incoming order trade at this book price? Market: always. Buy: price at or below the limit. Sell: at or above.
    /// </summary>
    bool IsPriceAcceptable(NewOrderRequest order, decimal bookPrice)
    {
        if (order.OrderType == OrderType.Market)
        {
            return true;
        }
        return order.Side == Side.Buy ? bookPrice <= order.LimitPrice : bookPrice >= order.LimitPrice;
    }
    /// <summary>
    /// Trades the incoming order against the opposite side, best price first and oldest order first.
    /// Adds 2 reports per fill (incoming + resting) and returns the quantity still unfilled.
    /// </summary>
    int Match(NewOrderRequest order, List<ExecutionReport> reports)
    {
        var oppositeList = order.Side == Side.Buy ? _asks : _bids;
        var remainingQty = order.Quantity;
        while (remainingQty > 0 && oppositeList.Count > 0)
        {
            decimal bestPrice = 0;
            LinkedList<RestingOrder>? priceLevelRestingOrders = null;
            foreach (var (price, restingOrders) in oppositeList)
            {
                bestPrice = price;
                priceLevelRestingOrders = restingOrders;
                break;
            }
            if (!IsPriceAcceptable(order, bestPrice))
            {
                break;
            }

            var restingOrder = priceLevelRestingOrders!.First!.Value;
            var fillQty = Math.Min(remainingQty, restingOrder.RemainingQty);
            restingOrder.RemainingQty -= fillQty;
            restingOrder.FilledQty += fillQty;
            remainingQty -= fillQty;

            if (restingOrder.RemainingQty == 0)
            {
                priceLevelRestingOrders.RemoveFirst();
                if (priceLevelRestingOrders.Count == 0)
                {
                    oppositeList.Remove(restingOrder.Price);
                    _levelPool.Push(priceLevelRestingOrders);
                }
                _ordersById.Remove(restingOrder.ClOrdID);
            }

            var orderStatus = remainingQty > 0 ? OrderStatus.PartiallyFilled : OrderStatus.Filled;
            reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                            ClOrdID: order.ClOrdID,
                                            Symbol: _symbol,
                                            Status: orderStatus,
                                            LastQty: fillQty,
                                            LastPrice: bestPrice,
                                            FilledQuantity: order.Quantity - remainingQty,
                                            LeavesQty: remainingQty,
                                            Reason: null));
            reports.Add(new ExecutionReport(ExecID: Guid.NewGuid(),
                                            ClOrdID: restingOrder.ClOrdID,
                                            Symbol: _symbol,
                                            Status: restingOrder.RemainingQty > 0 ? OrderStatus.PartiallyFilled : OrderStatus.Filled,
                                            LastQty: fillQty,
                                            LastPrice: bestPrice,
                                            FilledQuantity: restingOrder.FilledQty,
                                            LeavesQty: restingOrder.RemainingQty,
                                            Reason: null));
        }

        return remainingQty;
    }
    /// <summary>
    /// Is this order still waiting on the book?
    /// </summary>
    public bool Contains(Guid clOrdID)
    {
        return _ordersById.ContainsKey(clOrdID);
    }
    /// <summary>
    /// First (best) price on one side. Uses foreach instead of LINQ First() to avoid a boxed enumerator (hot path).
    /// </summary>
    private decimal GetBestPrice(Side side)
    {
        var sideRestingOrdersList = side == Side.Buy
            ? _bids
            : _asks;
        foreach (var (price, _) in sideRestingOrdersList)
        {
            return price;
        }

        return 0;
    }
}

/// <summary>
/// An order waiting on the book.
/// </summary>
public class RestingOrder(Guid clOrdID, Side side, decimal price, int remainingQty, int filledQty)
{
    public Guid ClOrdID { get; init; } = clOrdID;
    public Side Side { get; init; } = side;
    public decimal Price { get; init; } = price;
    public int RemainingQty { get; set; } = remainingQty;
    public int FilledQty { get; set; } = filledQty;
}