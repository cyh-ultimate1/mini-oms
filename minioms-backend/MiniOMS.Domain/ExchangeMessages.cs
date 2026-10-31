namespace MiniOMS.Domain;

public record NewOrderRequest(Guid ClOrdID, string Symbol, Side Side, OrderType OrderType, int Quantity, decimal? LimitPrice);
public record CancelRequest(Guid ClOrdID, string Symbol);
public record ExecutionReport(Guid ExecID, Guid ClOrdID, string Symbol, OrderStatus Status, int LastQty, decimal LastPrice, int FilledQuantity, int LeavesQty, string? Reason);