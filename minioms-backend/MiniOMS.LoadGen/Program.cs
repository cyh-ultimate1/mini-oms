using System.Collections.Concurrent;
using System.Diagnostics;
using HdrHistogram;
using MiniOMS.Domain;
using MiniOMS.Messaging;
using StackExchange.Redis;

// ---------- A. Settings ----------
// Usage: dotnet run -c Release --project MiniOMS.LoadGen -- [total] [rate] [redis]
//   rate = orders per second. 0 = flood (send as fast as possible, to find max throughput).
//          A rate below capacity measures the system's latency instead of queue waiting time.
var total = args.Length > 0 ? int.Parse(args[0]) : 10_000;
var rate = args.Length > 1 ? int.Parse(args[1]) : 0;
var redisAddress = args.Length > 2 ? args[2] : "localhost:6379";
var warmup = Math.Min(1_000, total / 10);          // sent but not measured (lets the JIT and caches warm up)

// ---------- B. Connect ----------
using var redis = await ConnectionMultiplexer.ConnectAsync(redisAddress);
var bus = new RedisBus(redis);

// ---------- C. Tracking ----------
var sentAt = new ConcurrentDictionary<Guid, long>();          // ClOrdID → send timestamp
var histogram = new LongHistogram(TimeStamp.Minutes(1), 3);   // latencies in Stopwatch ticks, 3 significant digits
var received = 0;
var rejected = 0;
var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

// ---------- D. Listen for events (before sending anything) ----------
// The ordered subscription calls this on one thread at a time, so the histogram is safe without locks.
await bus.SubscribeAsync<OmsEvent>(RedisKeys.Events, evt =>
{
    // Only the OMS's answer counts: Acknowledged (or Rejected). "New" is sent before risk/exchange.
    if (evt is not OrderUpdated o || o.Status is not (OrderStatus.Acknowledged or OrderStatus.Rejected))
    {
        return;
    }
    // Stop the clock. TryRemove also ignores orders that aren't ours and any later events for the same order.
    if (!sentAt.TryRemove(o.ClOrdID, out var t0))
    {
        return;
    }

    var latency = Stopwatch.GetTimestamp() - t0;
    if (o.Status == OrderStatus.Rejected)
    {
        rejected++;
    }

    var n = Interlocked.Increment(ref received);
    if (n > warmup)
    {
        histogram.RecordValue(latency);
    }
    if (n == total)
    {
        done.TrySetResult();
    }
});

// ---------- E. Send ----------
var mode = rate > 0 ? $"{rate:N0} orders/sec" : "flood (as fast as possible)";
Console.WriteLine($"Sending {total:N0} orders ({warmup:N0} warm-up) to {redisAddress}, mode: {mode} ...");
var clock = Stopwatch.StartNew();
var measureStart = 0L;
var ticksPerOrder = rate > 0 ? Stopwatch.Frequency / (double)rate : 0;   // gap between orders at the target rate
var startTimestamp = Stopwatch.GetTimestamp();

for (var i = 0; i < total; i++)
{
    if (i == warmup)
    {
        measureStart = clock.ElapsedTicks;                     // throughput is measured after warm-up
    }

    var clOrdId = Guid.NewGuid();
    // Buys @ 99.00 and sells @ 101.00 never cross: no fills, no position change, so no risk rejections.
    var isBuy = i % 2 == 0;
    var cmd = new PlaceOrder(Guid.NewGuid(), clOrdId, "ACME",
                             isBuy ? Side.Buy : Side.Sell, OrderType.Limit, 10,
                             isBuy ? 99.00m : 101.00m);

    long t0;
    if (rate > 0)
    {
        // Pace: wait until this order's planned send time.
        // Latency is measured from the PLANNED time, so a slow system can't hide its delays
        // by making us send later (this mistake is called "coordinated omission").
        t0 = startTimestamp + (long)(i * ticksPerOrder);
        while (Stopwatch.GetTimestamp() < t0)
        {
            Thread.Yield();
        }
    }
    else
    {
        t0 = Stopwatch.GetTimestamp();
    }

    sentAt[clOrdId] = t0;                                      // start the clock BEFORE publishing
    await bus.PublishAsync<OmsCommand>(RedisKeys.Commands, cmd);
}

try
{
    await done.Task.WaitAsync(TimeSpan.FromSeconds(60));       // wait for the last answers after sending finishes
}
catch (TimeoutException)
{
    Console.WriteLine($"Timed out: only {received:N0} of {total:N0} answers arrived. Is the OmsService running?");
    return;
}
clock.Stop();

// ---------- F. Report ----------
var measured = total - warmup;
var seconds = (clock.ElapsedTicks - measureStart) / (double)Stopwatch.Frequency;
double ToMicros(long ticks) => ticks / (Stopwatch.Frequency / 1_000_000.0);

Console.WriteLine();
Console.WriteLine($"Orders measured : {measured:N0}  (rejected: {rejected})");
Console.WriteLine($"Throughput      : {measured / seconds:N0} orders/sec");
Console.WriteLine($"p50             : {ToMicros(histogram.GetValueAtPercentile(50)):N0} µs");
Console.WriteLine($"p99             : {ToMicros(histogram.GetValueAtPercentile(99)):N0} µs");
Console.WriteLine($"p99.9           : {ToMicros(histogram.GetValueAtPercentile(99.9)):N0} µs");
Console.WriteLine($"max             : {ToMicros(histogram.GetMaxValue()):N0} µs");
