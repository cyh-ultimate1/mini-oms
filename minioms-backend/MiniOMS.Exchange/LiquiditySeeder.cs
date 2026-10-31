using MiniOMS.Domain;

namespace MiniOMS.Exchange;

/// <summary>
/// Fakes other traders so the user's orders can fill. It places 5 buy levels below and 5 sell levels
/// above the reference price (100 shares each, 0.05 apart), and refills used-up levels.
/// Seeder orders aren't tracked by the OMS.
/// </summary>
public class LiquiditySeeder
{
    public LiquiditySeeder(OrderBook book, SymbolInfo symbol)
    {
        _book = book;
        _symbol = symbol;
    }

    readonly OrderBook _book;
    readonly SymbolInfo _symbol;
    /// <summary>
    /// Price → the ID of the seeder's current order at that price.
    /// </summary>
    readonly Dictionary<decimal, Guid> _ordersByPrice = [];

    const int Levels = 5;
    const decimal Step = 0.05m;
    const int Qty = 100;
    
    /// <summary>
    /// Places the initial 5 buy levels and 5 sell levels around the reference price. Called once at startup.
    /// </summary>
    public void Seed()
    {
        for(int i = 1; i <= Levels; i++)
        {
            var buyPrice = _symbol.ReferencePrice - (i * Step);
            PlaceOrder(Side.Buy, buyPrice);

            var sellPrice = _symbol.ReferencePrice + (i * Step);
            PlaceOrder(Side.Sell, sellPrice);
        }
    }
    /// <summary>
    /// Refills any seeder level that has been fully used up. Skips a level if refilling it would
    /// trade against an existing order. The seeder only adds liquidity, it never takes it.
    /// </summary>
    public void TopUp()
    {
        foreach(var (price, orderID) in _ordersByPrice.ToList())
        {
            if (_book.Contains(orderID))
            {
                continue;
            }

            var side = price < _symbol.ReferencePrice ? Side.Buy : Side.Sell;

            // Never cross the book: a refill that would trade against a resting (user) order is skipped.
            if (side == Side.Buy && _book.BestAsk is decimal bestAsk && price >= bestAsk)
            {
                continue;
            }
            if (side == Side.Sell && _book.BestBid is decimal bestBid && price <= bestBid)
            {
                continue;
            }

            PlaceOrder(side, price);
        }
    }
    /// <summary>
    /// Sends one seeder limit order to the book and remembers its ID by price,
    /// so TopUp can check whether it's still there.
    /// </summary>
    void PlaceOrder(Side side, decimal price)
    {
        var newID = Guid.NewGuid();
        _book.AddOrder(new NewOrderRequest(newID, _symbol.Symbol, side, OrderType.Limit, Qty, price));
        _ordersByPrice[price] = newID;
    }
}
