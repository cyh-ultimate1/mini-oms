using MiniOMS.Domain;
using MiniOMS.Messaging;
using MiniOMS.WebApi;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Missing 'Redis' connection string");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connectionString));
builder.Services.AddSingleton<RedisBus>();
builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSignalR();
builder.Services.AddHostedService<EventForwarder>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/orders", async (PlaceOrderRequest req, IMessagePublisher publisher) =>
{
    if (req.ClOrdID == Guid.Empty || string.IsNullOrWhiteSpace(req.Symbol))
    {
        return Results.BadRequest("ClOrdID and Symbol are required");
    }

    var commandId = Guid.NewGuid();
    var cmd = new PlaceOrder(commandId, req.ClOrdID, req.Symbol, req.Side, req.OrderType, req.Quantity, req.LimitPrice);
    await publisher.PublishAsync<OmsCommand>(RedisKeys.Commands, cmd);

    return Results.Accepted(value: new CommandAccepted(req.ClOrdID, commandId));
});
app.MapDelete("/api/orders/{clOrdId:guid}", async (Guid clOrdId, IMessagePublisher publisher) =>
{
    var commandId = Guid.NewGuid();
    var cmd = new CancelOrder(commandId, clOrdId);
    await publisher.PublishAsync<OmsCommand>(RedisKeys.Commands, cmd);
    return Results.Accepted(value: new CommandAccepted(clOrdId, commandId));
});
app.MapGet("/api/orders", async (ISnapshotStore store) => Results.Ok(await store.GetAllAsync<OrderUpdated>(RedisKeys.Orders)));
app.MapGet("/api/positions", async (ISnapshotStore store) => Results.Ok(await store.GetAllAsync<PositionUpdated>(RedisKeys.Positions)));

app.MapHub<OrdersHub>("/hubs/orders");

app.Run();
