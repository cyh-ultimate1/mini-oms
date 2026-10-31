import type { PositionUpdated } from "../types";

export function PositionsPanel({
  positions,
}: {
  positions: Record<string, PositionUpdated>;
}) {
  const list = Object.values(positions).sort((a, b) =>
    a.symbol.localeCompare(b.symbol),
  );

  if (list.length <= 0) return <p>No positions</p>;
  return (
    <table>
      <thead>
        <tr>
          <th>Symbol</th>
          <th>Qty</th>
          <th>Avg Price</th>
          <th>Realized P&L</th>
        </tr>
      </thead>
      <tbody>
        {list.map((p) => (
          <tr key={p.symbol}>
            <td>{p.symbol}</td>
            <td>
              {p.quantity > 0 ? "+" : ""}
              {p.quantity}
            </td>
            <td>{p.avgPrice.toFixed(2)}</td>
            <td style={{ color: p.realizedPnl >= 0 ? "green" : "red" }}>
              {p.realizedPnl.toFixed(2)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
