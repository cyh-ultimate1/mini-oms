using Microsoft.AspNetCore.SignalR;
using MiniOMS.Domain;
using MiniOMS.Messaging;
using System.Threading.Channels;

namespace MiniOMS.WebApi;

/// <summary>
/// listens to Redis events and pushes them to every SignalR client.
/// </summary>
public class EventForwarder : BackgroundService
{
    public EventForwarder(IMessageSubscriber subscriber, IHubContext<OrdersHub> hub, ILogger<EventForwarder> logger)
    {
        _subscriber = subscriber;
        _hub = hub;
        _logger = logger;
    }

    private readonly IMessageSubscriber _subscriber;
    private readonly IHubContext<OrdersHub> _hub;
    private readonly ILogger<EventForwarder> _logger;
    private readonly Channel<OmsEvent> _events = Channel.CreateUnbounded<OmsEvent>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriber.SubscribeAsync<OmsEvent>(RedisKeys.Events, evt => _events.Writer.TryWrite(evt));
        _logger.LogInformation("Forwarding events to SignalR");
        await foreach(var evt in _events.Reader.ReadAllAsync(stoppingToken))
        {
            await ForwardAsync(evt);
        }
    }
    /// <summary>
    /// sends one event to all browsers.
    /// </summary>
    /// <param name="evt"></param>
    /// <returns></returns>
    private async Task ForwardAsync(OmsEvent evt)
    {
        try
        {
            var methodName = evt.GetType().Name;
            await _hub.Clients.All.SendAsync(methodName, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward {Event}", evt.GetType().Name);
        }
    }

    
}
