import type { ConnectionState } from "../hooks/useOms";

export function ConnectionStatus({ status }: { status: ConnectionState }) {
  const colours: Record<ConnectionState, string> = {
    Connected: "green",
    Connecting: "orange",
    Reconnecting: "orange",
    Disconnected: "red",
  };

  return <span style={{ color: colours[status] }}>● {status}</span>;
}
