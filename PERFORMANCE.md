# Performance

## Machine

| | |
|---|---|
| CPU | AMD Ryzen 7 5800H, 8 cores / 16 threads, 3.20 GHz |
| RAM | 28 GB usable |
| OS | Windows 11 (25H2) |
| Runtime | .NET 10.0.12, X64 RyuJIT, Release build |
| Tool | BenchmarkDotNet v0.15.8 with `MemoryDiagnoser` |

## How it was measured

- **In-process:** BenchmarkDotNet runs each method millions of times after a warmup, in a Release build.
- **Book setup:** ACME with 5 bid levels (98.60–99.00) and 5 ask levels (102.00–102.40), 100 shares each.
- **Each benchmark leaves the book as it found it**, so every run measures the same state.

| Benchmark | What it does |
|---|---|
| `RiskChecker.Check` | One full pre-trade check on a valid limit order (passes) |
| `AddAndCancel` | Rest a limit buy @ 100.00, then cancel it |
| `RestThenMatch` | Rest a limit sell @ 100.00, then a limit buy @ 100.00 fully fills it |

## In-process results

### Before optimization (baseline)

| Benchmark | Mean | Allocated |
|---|---:|---:|
| Risk Check | 45.96 ns | 96 B |
| AddAndCancel | 300.4 ns | 664 B |
| RestThenMatch | 497.2 ns | 1,064 B |

**Allocation sources found:**

| Where | What's allocated per call |
|---|---|
| `RiskChecker.Check` | A new `RiskResult` for every pass; a closure + delegate from LINQ `FirstOrDefault` with a captured lambda |
| `AddOrder` / `CancelOrder` | A new `List` (and its array) for the reports, plus each `ExecutionReport` |
| Resting at a new price | `LinkedList` + `LinkedListNode` + `RestingOrder` + a dictionary entry |
| `Match`, `BestBid`, `BestAsk` | A boxed enumerator from LINQ `.First()` on `SortedDictionary` / `LinkedList` |

### After optimization

| Benchmark | Mean (before → after) | Change | Allocated (before → after) | Change |
|---|---:|---:|---:|---:|
| Risk Check | 45.96 → 33.82 ns | **−26%** | 96 B → **0 B** | **−100%** |
| AddAndCancel | 300.4 → 306.7 ns | ≈ same | 664 → 456 B | **−31%** |
| RestThenMatch | 497.2 → 445.5 ns | **−10%** | 1,064 → 736 B | **−31%** |

**What was changed:**

| Fix | Change | Removed |
|---|---|---|
| Risk | Shared static `RiskResult` for passes; `Dictionary` symbol lookup instead of LINQ with a captured lambda | New result object + closure + delegate per check |
| A | `foreach` + `break` instead of LINQ `.First()`; `LinkedList.First` property instead of `.First()` | Boxed enumerators in `Match`, `BestBid`, `BestAsk` |
| B | One reused `List<ExecutionReport>` per book | A new list (and array) per `AddOrder` / `CancelOrder` |
| C | A pool (`Stack`) of empty price levels | A new `LinkedList` each time a price level empties and refills |

**Notes:**
- `AddAndCancel` time is unchanged (within noise), but it creates **31% less garbage**, which means fewer GC pauses under load (better p99).
- **Still allocated:** each `ExecutionReport` (a class), the `LinkedListNode` + `RestingOrder` for every resting order, and `SortedDictionary` entries for new price levels. Removing these would need bigger design changes (struct reports, node pooling), so they're left as future improvements.

## End-to-end results (load generator)

### How it was measured

- **Tool:** `MiniOMS.LoadGen` (Release build, run on the host), which publishes `PlaceOrder` straight to Redis.
- **System:** Redis + OmsService + WebApi in Docker Desktop (WSL2), with OmsService as a Release build. The OmsService was restarted before each run (fresh memory).
- **Latency** = from the order's send time to its first **Acknowledged** (or Rejected) `OrderUpdated` event, recorded in HdrHistogram.
- **Orders:** 10,000 limit orders on ACME (buys @ 99.00, sells @ 101.00, so none cross and there are no fills). The first 1,000 are warm-up and aren't measured.
- **Two modes:**
  - **Flood** (`rate 0`): send as fast as possible, to find the **max throughput**.
  - **Steady rate** (`rate 500`): send 500 orders/sec, below capacity, to measure the system's **latency**. It's measured from the **planned** send time, to avoid coordinated omission.

### Before optimization (baseline)

| Mode | Throughput | p50 | p99 | p99.9 | max |
|---|---:|---:|---:|---:|---:|
| Flood | **1,338 orders/sec** | 856 ms | 1,730 ms | 1,737 ms | 1,738 ms |
| 500 orders/sec | 500 orders/sec | **1.7 ms** | 41.4 ms | 73.1 ms | 77.6 ms |

**Findings:**
- In flood mode the OMS can't keep up, so orders **queue**, and the latency is mostly **waiting in line**. That's why p50 is almost a second.
- **The bottleneck is not matching** (about 0.0005 ms per order, from BenchmarkDotNet). It's `OmsWorker` **awaiting each Redis call one after another**: a snapshot save + publish for each of the 2–3 events per order, which is about 4–6 round trips per order.

### After optimization

**Change:** `OmsWorker` no longer awaits each Redis call one by one. For each command it **starts** every snapshot save and publish, then awaits them **all at once** (`Task.WhenAll`). StackExchange.Redis still sends them **in order** over the one connection (pipelining), so "save before publish" and event order are kept. This cuts about 4–6 waits per order down to 1.

| Mode | Throughput (before → after) | p50 | p99 | p99.9 | max |
|---|---:|---:|---:|---:|---:|
| Flood | 1,338 → **1,712 orders/sec** (+28%) | 856 → **42 ms** | 1,730 → **202 ms** | 1,737 → **208 ms** | 1,738 → **211 ms** |
| 500 orders/sec | 500 → 500 | 1.7 → **1.2 ms** | 41.4 → **4.4 ms** | 73.1 → **16.1 ms** | 77.6 → **23.2 ms** |

**Findings:**
- At a steady 500 orders/sec, **p99 improved about 9×** (41 → 4.4 ms). Fewer waits per order means the queue rarely backs up.
- In flood mode, latency is still mostly queueing (the OMS is still slower than the sender), but the queue drains much faster.
- **Remaining bottleneck:** each command still waits for its own Redis round trip before the next command starts. The next step would be to **batch several commands** per wait, or use fire-and-forget publishing. Docker Desktop on Windows (WSL2 networking) also adds latency compared with a native Linux host.
