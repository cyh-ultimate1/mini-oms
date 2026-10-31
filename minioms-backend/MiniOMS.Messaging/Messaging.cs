namespace MiniOMS.Messaging;

public interface IMessagePublisher
{
    /// <summary>
    /// sends one message to a channel.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="channel"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    Task PublishAsync<T>(string channel, T message);
}
public interface IMessageSubscriber
{
    /// <summary>
    /// listens on a channel and calls handler for each message.
    /// Deliver messages one at a time, in the order they were published.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="channel"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    Task SubscribeAsync<T>(string channel, Action<T> handler);
}
public interface ISnapshotStore
{
    /// <summary>
    /// saves the latest snapshot of one order or position.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="hashKey">refers to which dictionary.</param>
    /// <param name="field">refers to key inside the dictionary.</param>
    /// <param name="value">refers to value of the key.</param>
    /// <returns></returns>
    Task SaveAsync<T>(string hashKey, string field, T value);
    /// <summary>
    /// reads every snapshot in a hash.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="hashKey"></param>
    /// <returns></returns>
    Task<IReadOnlyList<T>> GetAllAsync<T>(string hashKey);
}

public static class RedisKeys
{
    public const string Commands = "minioms:commands";
    public const string Events = "minioms:events";
    public const string Orders = "minioms:orders";
    public const string Positions = "minioms:positions";
}
