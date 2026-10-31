using Microsoft.AspNetCore.SignalR.Client;
using MiniOMS.Domain;

var url = args.Length > 0 ? args[0] : "http://localhost:5244/hubs/orders";
var connection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();
connection.On<OrderUpdated>(nameof(OrderUpdated), e => Console.WriteLine($"[{e.Sequence}] ORDER {e.ClOrdID} {e.Status} {e.FilledQuantity}/{e.Quantity} @ {e.AvgFillPrice}"));
connection.On<PositionUpdated>(nameof(PositionUpdated), e => Console.WriteLine($"[{e.Sequence}] POSITION {e.Symbol} qty {e.Quantity} @ {e.AvgPrice} pnl {e.RealizedPnl}"));
connection.On<RiskAlert>(nameof(RiskAlert), e => Console.WriteLine($"[{e.Sequence}] RISK {e.ClOrdID} {e.Rule}: {e.Message}"));
connection.On<CommandRejected>(nameof(CommandRejected), e => Console.WriteLine($"[{e.Sequence}] REJECTED {e.ClOrdID}: {e.Reason}"));
connection.Reconnecting += (_) =>
{
    Console.WriteLine("Reconnecting...");
    return Task.CompletedTask;
};
connection.Reconnected += (_) =>
{
    Console.WriteLine("Reconnected...");
    return Task.CompletedTask;
};
await connection.StartAsync();
Console.WriteLine($"Connected to {url}. Press Enter to quit.");
Console.ReadLine();
await connection.StopAsync();