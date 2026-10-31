import type { CommandAccepted, OrderUpdated, PlaceOrderRequest, PositionUpdated } from "./types";

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  if (!res.ok) {
    throw new Error(`${res.status}: ${await res.text()}`);
  }
  return (await res.json()) as T;
}

export function placeOrder(req: PlaceOrderRequest): Promise<CommandAccepted> {
  return request<CommandAccepted>("/api/orders", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function cancelOrder(clOrdID: string): Promise<CommandAccepted> {
  return request<CommandAccepted>(`/api/orders/${clOrdID}`, {
    method: "DELETE",
  });
}

export function getOrders(): Promise<OrderUpdated[]> {
  return request<OrderUpdated[]>("/api/orders", {
    method: "GET",
  });
}

export function getPositions(): Promise<PositionUpdated[]> {
  return request<PositionUpdated[]>("/api/positions", {
    method: "GET",
  });
}
