using StackExchange.Redis;
using System.Text.Json;

namespace MiniOMS.Messaging;

public class RedisBus : IMessagePublisher, IMessageSubscriber, ISnapshotStore
{
    public RedisBus(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }
    private readonly IConnectionMultiplexer _redis;

    public async Task PublishAsync<T>(string channel, T message)
    {
        var json = JsonSerializer.Serialize(message);
        var subscriber = GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(channel), json);
    }

    public async Task SubscribeAsync<T>(string channel, Action<T> handler)
    {
        var subscriber = GetSubscriber();
        // use the queue to deliver messages one at a time, in the order they were published.
        // handler-style does not care about order.
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(channel));
        queue.OnMessage(channelMessage =>
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(channelMessage.Message.ToString());
                if (result is not null)
                {
                    handler(result);
                }
            }
            catch (JsonException)
            {
                //do nothing. A bad message must not crash the service.
            }
        });
    }


    public async Task SaveAsync<T>(string hashKey, string field, T value)
    {
        var json = JsonSerializer.Serialize(value);
        var redisDB = _redis.GetDatabase();
        await redisDB.HashSetAsync(hashKey, field, json);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string hashKey)
    {
        var redisDB = _redis.GetDatabase();
        var entries = await redisDB.HashGetAllAsync(hashKey);

        var snapshots = new List<T>();
        foreach(var entry in entries)
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(entry.Value.ToString());
                if (result is not null)
                {
                    snapshots.Add(result);
                }
            }
            catch (JsonException)
            {
                //continue
            }
        }

        return snapshots;
    }

    private ISubscriber GetSubscriber()
    {
        return _redis.GetSubscriber();
    }
}
