using MiniOMS.Domain;

namespace MiniOMS.WebApi;

public record PlaceOrderRequest(Guid ClOrdID, string Symbol, Side Side, OrderType OrderType, int Quantity, decimal? LimitPrice);
public record CommandAccepted(Guid ClOrdID, Guid CommandId);
