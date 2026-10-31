using System.Text.Json.Serialization;

namespace MiniOMS.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<OrderType>))]
public enum OrderType
{
    Limit, Market
}

[JsonConverter(typeof(JsonStringEnumConverter<Side>))]
public enum Side
{
    Buy,
    Sell,
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    New, Acknowledged, PartiallyFilled, Filled, Cancelled, Rejected
}