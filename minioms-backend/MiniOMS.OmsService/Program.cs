using MiniOMS.Core;
using MiniOMS.Messaging;
using MiniOMS.OmsService;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Missing 'Redis' connection string");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connectionString));
builder.Services.AddSingleton<RedisBus>();
builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisBus>());
builder.Services.AddSingleton<InProcessExchangeGateway>();
builder.Services.AddSingleton<IExchangeGateway>(sp => sp.GetRequiredService<InProcessExchangeGateway>());
builder.Services.AddSingleton(new RiskChecker(new RiskLimits()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OmsEngine>();
builder.Services.AddHostedService<OmsWorker>();

var host = builder.Build();
host.Run();
