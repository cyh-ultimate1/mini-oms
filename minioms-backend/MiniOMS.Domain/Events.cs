using System.Text.Json.Serialization;

namespace MiniOMS.Domain;

/// <summary>
/// A fact the OMS announces about something that already happened
/// (an order updated, a position changed, a risk alert). Sent to the UI; it can't be refused.
/// Every event has a Sequence (for ordering and de-duplication) and a TimestampUtc.
/// </summary>
/// 
[JsonPolymorphic(TypeDiscriminatorPropertyName = "EventType")]
[JsonDerivedType(typeof(OrderUpdated), "OrderUpdated")]
[JsonDerivedType(typeof(PositionUpdated), "PositionUpdated")]
[JsonDerivedType(typeof(RiskAlert), "RiskAlert")]
[JsonDerivedType(typeof(CommandRejected), "CommandRejected")]
public abstract record OmsEvent
{
    public long Sequence { get; init; }
    public DateTime TimestampUtc { get; init; }
}
public record OrderUpdated(Guid ClOrdID,
                           string Symbol,
                           Side Side,
                           OrderType OrderType,
                           int Quantity,
                           decimal? LimitPrice,
                           OrderStatus Status,
                           int FilledQuantity,
                           decimal AvgFillPrice,
                           string? Reason) : OmsEvent;
public record PositionUpdated(string Symbol, int Quantity, decimal AvgPrice, decimal RealizedPnl) : OmsEvent;
public record RiskAlert(Guid ClOrdID, string Rule, string Message) : OmsEvent;
public record CommandRejected(Guid CommandId, Guid ClOrdID, string Reason) : OmsEvent;