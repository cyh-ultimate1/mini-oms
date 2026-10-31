import { useEffect, useMemo, useRef, useState } from "react";
import type {
  CommandRejected,
  OrderRow,
  OrderUpdated,
  PlaceOrderRequest,
  PositionUpdated,
  RiskAlert,
} from "../types";
import { cancelOrder, getOrders, getPositions, placeOrder } from "../api";
import { HubConnectionBuilder } from "@microsoft/signalr";

export type ConnectionState =
  | "Connecting"
  | "Connected"
  | "Reconnecting"
  | "Disconnected";

export function useOms() {
  const [orders, setOrders] = useState<Record<string, OrderUpdated>>({});
  const [positions, setPositions] = useState<Record<string, PositionUpdated>>(
    {},
  );
  const [status, setStatus] = useState<ConnectionState>("Connecting");
  const [pending, setPending] = useState<
    Record<string, { req: PlaceOrderRequest; sentAt: number }>
  >({});
  const [now, setNow] = useState<number>(Date.now);
  const buffer = useRef<{
    orders: Record<string, OrderUpdated>;
    positions: Record<string, PositionUpdated>;
  }>({ orders: {}, positions: {} });

  useEffect(() => {
    let active = true;

    async function loadSnapshots() {
      const [o, p] = await Promise.all([getOrders(), getPositions()]);

      setOrders((prev) =>
        mergeNewer(prev, Object.fromEntries(o.map((x) => [x.clOrdID, x]))),
      );
      setPositions((prev) =>
        mergeNewer(prev, Object.fromEntries(p.map((x) => [x.symbol, x]))),
      );

      // setOrders(Object.fromEntries(o.map((x) => [x.clOrdID, x])));
      // setPositions(Object.fromEntries(p.map((x) => [x.symbol, x])));
    }

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/orders")
      .withAutomaticReconnect()
      .build();

    connection.on("OrderUpdated", (o: OrderUpdated) => {
      buffer.current.orders[o.clOrdID] = o;
    });
    connection.on("PositionUpdated", (p: PositionUpdated) => {
      buffer.current.positions[p.symbol] = p;
    });

    // connection.on("OrderUpdated", (o: OrderUpdated) => {
    //   setOrders((prev) => ({ ...prev, [o.clOrdID]: o }));
    //   setPending((prev) => {
    //     const { [o.clOrdID]: _, ...rest } = prev;
    //     return rest;
    //   });
    // });
    connection.on("RiskAlert", (r: RiskAlert) =>
      console.warn("Risk", r.rule, r.message),
    );
    connection.on("CommandRejected", (c: CommandRejected) =>
      console.warn("Rejected", c.reason),
    );
    // connection.on("PositionUpdated", (p: PositionUpdated) =>
    //   setPositions((prev) => ({ ...prev, [p.symbol]: p })),
    // );

    connection.onreconnecting(() => setStatus("Reconnecting"));
    connection.onreconnected(() => {
      setStatus("Connected");
      loadSnapshots();
    });
    connection.onclose(() => {
      if (active) setStatus("Disconnected");
    });
    connection
      .start()
      .then(() => {
        if (!active) return;
        setStatus("Connected");
        return loadSnapshots();
      })
      .catch(() => {
        if (active) setStatus("Disconnected");
      });

    return () => {
      active = false;
      connection.stop();
    };
  }, []);

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const clOrdID = setInterval(() => {
      const { orders: o, positions: p } = buffer.current;
      if (Object.keys(o).length === 0 && Object.keys(p).length === 0) return; // nothing new, no redraw
      buffer.current = { orders: {}, positions: {} }; // start a fresh buffer
      setOrders((prev) => mergeNewer(prev, o));
      setPositions((prev) => mergeNewer(prev, p));
      setPending((prev) => {
        const next = { ...prev };
        for (const id of Object.keys(o)) delete next[id];
        return next;
      });
    }, 150);
    return () => clearInterval(clOrdID);
  }, []);

  async function submitOrder(req: PlaceOrderRequest) {
    setPending((prev) => ({
      ...prev,
      [req.clOrdID]: { req, sentAt: Date.now() },
    }));
    try {
      await placeOrder(req);
    } catch (err) {
      setPending((prev) => {
        const { [req.clOrdID]: _, ...rest } = prev;
        return rest;
      });
      throw err;
    }
  }

  const rows = useMemo<OrderRow[]>(() => {
    const fromServer = Object.values(orders); // OrderUpdated already fits OrderRow
    const fromPending = Object.values(pending)
      .filter((p) => !orders[p.req.clOrdID])
      .map(
        (p) =>
          ({
            ...p.req,
            status: now - p.sentAt > 5000 ? "Unconfirmed" : "Pending",
            filledQuantity: 0,
            avgFillPrice: 0,
            reason: null,
          }) as OrderRow,
      );
    return [...fromPending, ...fromServer];
  }, [orders, pending, now]);

  return { rows, positions, status, submitOrder, cancelOrder };
}

function mergeNewer<T extends { sequence: number }>(
  prev: Record<string, T>,
  incoming: Record<string, T>,
) {
  const next = { ...prev };
  for (const [key, item] of Object.entries(incoming)) {
    if (!next[key] || item.sequence > next[key].sequence) next[key] = item;
  }
  return next;
}
