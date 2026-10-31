using MiniOMS.Domain;

namespace MiniOMS.Core;

public class Position
{
    public Position(string symbol)
    {
        Symbol = symbol;
    }

    public string Symbol { get; }
    /// <summary>
    /// signed, where + means long and − means short
    /// </summary>
    public int Quantity { get; private set; }
    public decimal AvgPrice { get; private set; }
    public decimal RealizedPnl { get; private set; }

    /// <summary>
    /// Updates the position after one fill.
    /// Same direction (opening or adding): the average price becomes the weighted average.
    /// Opposite direction (reducing, closing or flipping): realized P&amp;L is booked on the closed shares.
    /// If the position flips, the leftover opens at the fill price.
    /// </summary>
    public void ApplyFill(Side side, int qty, decimal price)
    {
        // + for a buy, − for a sell
        var signedQty = side == Side.Buy ? qty : -qty;
        var isSameDirection = Quantity == 0 || (Quantity > 0 && signedQty > 0) || (Quantity < 0 && signedQty < 0);

        // Opening or adding: weighted average price
        if (isSameDirection)
        {
            AvgPrice = (AvgPrice * Math.Abs(Quantity) + price * qty) / (Math.Abs(Quantity) + qty);
            Quantity += signedQty;
            return;
        }

        // Reducing, closing or flipping: book P&L on the closed shares
        // (× direction: long profits when the price rises, short profits when it falls)
        var closedQty = Math.Min(qty, Math.Abs(Quantity));
        var direction = Quantity > 0 ? 1 : -1;
        RealizedPnl += (closedQty * (price - AvgPrice) * direction);
        Quantity += signedQty;

        // Fully closed: no position, so no average price
        if (Quantity == 0)
        {
            AvgPrice = 0;
        }
        // Flipped: the leftover opened a new position at this fill's price
        else if (qty > closedQty)
        {
            AvgPrice = price;
        }
        // Only reduced: the average price is unchanged
    }
}
