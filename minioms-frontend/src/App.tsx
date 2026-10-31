import "./App.css";
import { ConnectionStatus } from "./components/ConnectionStatus";
import { OrderForm } from "./components/OrderForm";
import { OrdersGrid } from "./components/OrdersGrid";
import { PositionsPanel } from "./components/PositionsPanel";
import { useOms } from "./hooks/useOms";

export default function App() {
  const { rows, positions, status, submitOrder, cancelOrder } = useOms();
  return (
    <>
      <ConnectionStatus status={status} />
      <h1>Mini OMS</h1>
      <OrderForm onSubmit={submitOrder} />
      <OrdersGrid rows={rows} onCancel={cancelOrder} />
      <PositionsPanel positions={positions} />

      {/* <p>
        {status}: {Object.keys(orders).length} orders,{" "}
        {Object.keys(positions).length} positions
      </p> */}
    </>
  );
}
