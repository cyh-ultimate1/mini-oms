using MiniOMS.Domain;

namespace MiniOMS.Core;

public record RiskLimits(int MaxOrderQty = 1000, decimal PriceBandPercent = 0.05m, int MaxPosition = 5000);
public record RiskResult(bool Passed, string? Rule, string? Message)
{
    //_pass can't be changed after it's created. so everyone can share the same object.
    private static readonly RiskResult _pass = new(true, null, null);
    public static RiskResult Pass() => _pass;
    public static RiskResult Fail(string rule, string message) => new RiskResult(false, rule, message);
}
public class RiskChecker
{
    public RiskChecker(RiskLimits limits)
    {
        _limits = limits;
    }
    
    private readonly RiskLimits _limits;

    public RiskResult Check(Order order, int currentPosition)
    {
        if(!Symbols.BySymbol.TryGetValue(order.Symbol, out var orderSymbolInfo))
        {
            return RiskResult.Fail("UnknownSymbol", $"Unknown symbol '{order.Symbol}'");
        }

        if (order.Quantity <= 0)
            return RiskResult.Fail("InvalidQuantity", $"Quantity must be greater than 0 (was {order.Quantity})");

        if(order.OrderType == OrderType.Limit)
        {
            if (order.LimitPrice is null or <= 0)
                return RiskResult.Fail("InvalidPrice", "Limit order needs a price greater than 0");

            if (order.LimitPrice % orderSymbolInfo.TickSize != 0)
                return RiskResult.Fail("InvalidTick", $"Price {order.LimitPrice} is not on a {orderSymbolInfo.TickSize} tick");
        }

        if (order.Quantity > _limits.MaxOrderQty)
            return RiskResult.Fail("MaxOrderQuantity", $"Quantity {order.Quantity} exceeds max {_limits.MaxOrderQty}");

        if (order.OrderType == OrderType.Limit)
        {
            var band = orderSymbolInfo.ReferencePrice * _limits.PriceBandPercent;
            if(Math.Abs(order.LimitPrice!.Value - orderSymbolInfo.ReferencePrice) > band)
            {
                return RiskResult.Fail("PriceBand", $"Price {order.LimitPrice} is outside ±{_limits.PriceBandPercent:P0} of reference {orderSymbolInfo.ReferencePrice}");
            }
        }
        var signedQty = order.Side == Side.Buy ? order.Quantity : -order.Quantity;
        var newPosition = currentPosition + signedQty;
        if (Math.Abs(newPosition) > _limits.MaxPosition)
        {
            return RiskResult.Fail("MaxPosition", $"New position {newPosition} would exceed max {_limits.MaxPosition}");
        }

        return RiskResult.Pass();
    }
}
