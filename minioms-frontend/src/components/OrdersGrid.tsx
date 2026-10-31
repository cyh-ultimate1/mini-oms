import {
  AllCommunityModule,
  ModuleRegistry,
  type ColDef,
  type ICellRendererParams,
} from "ag-grid-community";
import { AgGridReact } from "ag-grid-react";
import type { OrderRow } from "../types";

// AG Grid 36 only loads the features you register. Done once, when the file is first imported.
ModuleRegistry.registerModules([AllCommunityModule]);

// Only these orders are still live on the book, so only these can be cancelled.
const cancellable = new Set(["Acknowledged", "PartiallyFilled"]);

// Data columns. Defined OUTSIDE the component so the array isn't recreated on every redraw.
// `field` must match an OrderRow property name; the grid reads that value for each row.
const dataColumns: ColDef<OrderRow>[] = [
  { field: "symbol", width: 90 },
  { field: "side", width: 80 },
  { field: "orderType", headerName: "Type", width: 90 },
  { field: "quantity", headerName: "Qty", width: 80 },
  { field: "limitPrice", headerName: "Price", width: 90 },
  { field: "status", width: 140 },
  { field: "filledQuantity", headerName: "Filled", width: 80 },
  { field: "avgFillPrice", headerName: "Avg Px", width: 90 },
  { field: "reason", flex: 1 },                       // flex: takes the remaining width
];

// Props = the component's inputs, given by App.tsx.
interface Props {
  rows: OrderRow[];
  onCancel: (clOrdID: string) => void;
}

export function OrdersGrid({ rows, onCancel }: Props) {
  // The Cancel column needs `onCancel` (a prop), so it's built INSIDE the component.
  const cancelColumn: ColDef<OrderRow> = {
    headerName: "",
    width: 90,
    // The cell's "value" is the status, so AG Grid redraws the button when the status changes.
    // (Without a value, the grid thinks this cell never changes and keeps the old content.)
    valueGetter: (p) => p.data?.status,
    // cellRenderer: instead of showing a value, draw our own content in each cell.
    cellRenderer: (p: ICellRendererParams<OrderRow>) =>
      p.data && cancellable.has(p.data.status) ? (
        <button onClick={() => onCancel(p.data!.clOrdID)}>Cancel</button>
      ) : null, // null = draw nothing (finished or pending orders)
  };

  return (
    // AG Grid needs a container with a fixed height, or it draws nothing.
    <div style={{ height: 400 }}>
      <AgGridReact<OrderRow>
        rowData={rows}                                 // the data: one object per row
        columnDefs={[...dataColumns, cancelColumn]}    // the columns, in order
        getRowId={(p) => p.data.clOrdID}               // which row is which, so changed rows update IN PLACE
      />
    </div>
  );
}
