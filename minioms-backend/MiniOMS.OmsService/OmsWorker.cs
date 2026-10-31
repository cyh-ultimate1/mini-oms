using System.Threading.Channels;
using MiniOMS.Core;
using MiniOMS.Domain;
using MiniOMS.Exchange;
using MiniOMS.Messaging;

namespace MiniOMS.OmsService;

/// <summary>
/// Internal command used only inside this service, to put seeder top-up ticks into the same queue.
/// </summary>
internal sealed record TopUpLiquidity() : OmsCommand(Guid.Empty);

public class OmsWorker : BackgroundService
{
    public OmsWorker(OmsEngine engine,
                     InProcessExchangeGateway gateway,
                     IMessageSubscriber subscriber,
                     IMessagePublisher publisher,
                     ISnapshotStore snapshots,
                     ILogger<OmsWorker> logger)
    {
        _engine = engine;
        _gateway = gateway;
        _subscriber = subscriber;
        _publisher = publisher;
        _snapshots = snapshots;
        _logger = logger;
    }

    private readonly OmsEngine _engine;
    private readonly InProcessExchangeGateway _gateway;
    private readonly IMessageSubscriber _subscriber;
    private readonly IMessagePublisher _publisher;
    private readonly ISnapshotStore _snapshots;
    private readonly ILogger<OmsWorker> _logger;
    // The single queue: Redis commands and top-up ticks both go in here, and ONE loop reads them.
    private readonly Channel<OmsCommand> _commands = Channel.CreateUnbounded<OmsCommand>();
    private readonly List<LiquiditySeeder> _seeders = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach(var symbolInfo in Symbols.All)
        {
            var seeder = new LiquiditySeeder(_gateway.GetBook(symbolInfo.Symbol), symbolInfo);
            seeder.Seed();
            _seeders.Add(seeder);
        }

        await _subscriber.SubscribeAsync<OmsCommand>(RedisKeys.Commands, cmd => _commands.Writer.TryWrite(cmd));
        _ = RunTopUpTimerAsync(stoppingToken);
        _logger.LogInformation("OMS Service ready");

        await foreach(var cmd in _commands.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (cmd is TopUpLiquidity)
                {
                    foreach (var seeder in _seeders)
                    {
                        seeder.TopUp();
                    }
                    continue;
                }
                var events = _engine.Handle(cmd);

                var pendingTasks = new List<Task>();
                foreach (var evt in events)
                {
                    pendingTasks.AddRange(PublishAndSaveAsync(evt));
                }
                await Task.WhenAll(pendingTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle {Command}", cmd.GetType().Name);
            }
        }
    }
    /// <summary>
    /// puts a top-up into the queue every 2 seconds.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task RunTopUpTimerAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await _commands.Writer.WriteAsync(new TopUpLiquidity());
        }
    }
    /// <summary>
    /// saves the snapshot, then announces the event.
    /// Why save before publishing? If the UI gets the event and immediately reloads, the snapshot is already up to date.
    /// </summary>
    /// <param name="evt"></param>
    /// <returns></returns>
    private List<Task> PublishAndSaveAsync(OmsEvent evt)
    {
        var tasks = new List<Task>();
        if(evt is OrderUpdated o)
        {
            tasks.Add(_snapshots.SaveAsync(RedisKeys.Orders, o.ClOrdID.ToString(), o));
        }else if(evt is PositionUpdated p)
        {
            tasks.Add(_snapshots.SaveAsync(RedisKeys.Positions, p.Symbol, p));
        }

        tasks.Add(_publisher.PublishAsync<OmsEvent>(RedisKeys.Events, evt));

        return tasks;
    }
}
