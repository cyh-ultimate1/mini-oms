import { useState } from "react";
import type { OrderType, PlaceOrderRequest, Side } from "../types";

export function OrderForm({ onSubmit }: { onSubmit: (req: PlaceOrderRequest) => Promise<void> }) {
  const [symbol, setSymbol] = useState("ACME");
  const [side, setSide] = useState<Side>("Buy");
  const [orderType, setOrderType] = useState<OrderType>("Limit");
  const [quantity, setQuantity] = useState("");
  const [limitPrice, setLimitPrice] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const req: PlaceOrderRequest = {
      clOrdID: crypto.randomUUID(),
      symbol,
      side,
      orderType,
      quantity: Number(quantity),
      limitPrice: orderType === "Limit" ? Number(limitPrice) : null,
    };

    try {
    //   await placeOrder(req);
      await onSubmit(req);
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <select value={symbol} onChange={(e) => setSymbol(e.target.value)}>
        <option>ACME</option>
        <option>BOLT</option>
        <option>CRUX</option>
      </select>
      <select value={side} onChange={(e) => setSide(e.target.value as Side)}>
        <option>Buy</option>
        <option>Sell</option>
      </select>
      <select
        value={orderType}
        onChange={(e) => setOrderType(e.target.value as OrderType)}
      >
        <option>Limit</option>
        <option>Market</option>
      </select>
      <input
        type="number"
        min={1}
        value={quantity}
        onChange={(e) => setQuantity(e.target.value)}
        required
        placeholder="Qty"
      />
      <input
        type="number"
        step="0.01"
        disabled={orderType === "Market"}
        value={limitPrice}
        onChange={(e) => setLimitPrice(e.target.value)}
        required
        placeholder="Price"
      />
      <button type="submit">Submit</button>
      {error && <p>{error}</p>}
    </form>
  );
}
