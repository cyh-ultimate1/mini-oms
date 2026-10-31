using System.Text.Json.Serialization;

namespace MiniOMS.Domain;

/// <summary>
/// A request to the OMS to do something (place or cancel an order).
/// Sent by the UI; the OMS may accept or reject it.
/// </summary>
/// 
[JsonPolymorphic(TypeDiscriminatorPropertyName = "CommandType")]
[JsonDerivedType(typeof(PlaceOrder), "PlaceOrder")]
[JsonDerivedType(typeof(CancelOrder), "CancelOrder")]
public abstract record OmsCommand(Guid CommandId);
public record PlaceOrder(Guid CommandId,
                         Guid ClOrdID,
                         string Symbol,
                         Side Side,
                         OrderType OrderType,
                         int Quantity,
                         decimal? LimitPrice) : OmsCommand(CommandId);
public record CancelOrder(Guid CommandId, Guid ClOrdID) : OmsCommand(CommandId);
