// TypeScript copies of the shapes in CONTRACTS.md (camelCase, as REST and SignalR send them).

export type Side = "Buy" | "Sell";
export type OrderType = "Limit" | "Market";
export type OrderStatus =
  | "New"
  | "Acknowledged"
  | "PartiallyFilled"
  | "Filled"
  | "Cancelled"
  | "Rejected";
// Pending and Unconfirmed exist only in the UI; the server never sends them.
export type UiStatus = OrderStatus | "Pending" | "Unconfirmed";


export interface EventBase {
  sequence: number;
  timestampUtc: string;
}

export interface PositionUpdated extends EventBase {
  symbol: string;
  quantity: number;
  avgPrice: number;
  realizedPnl: number;
}

export interface OrderUpdated extends EventBase {
  clOrdID: string;
  symbol: string;
  side: Side;
  orderType: OrderType;
  quantity: number;
  limitPrice: number | null;
  status: OrderStatus;
  filledQuantity: number;
  avgFillPrice: number;
  reason: string | null;
}

export interface RiskAlert extends EventBase {
  clOrdID: string;
  rule: string;
  message: string;
}

export interface CommandRejected extends EventBase {
  commandId: string;
  clOrdID: string;
  reason: string;
}


export interface PlaceOrderRequest {
  clOrdID: string;
  symbol: string;
  side: Side;
  orderType: OrderType;
  quantity: number;
  limitPrice: number | null;
}

export interface CommandAccepted {
  clOrdID: string;
  commandId: string;
}

export interface OrderRow {
  clOrdID: string; symbol: string; side: Side; orderType: OrderType;
  quantity: number; limitPrice: number | null;
  status: UiStatus; filledQuantity: number; avgFillPrice: number;
  reason: string | null;
}
