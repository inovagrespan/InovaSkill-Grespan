import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { getAuthToken } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";

const HUB_URL = buildServiceUrl("hubs/file-jobs");

type JobUpdatedPayload = {
  jobId: number;
};

let connection: HubConnection | null = null;
let startPromise: Promise<void> | null = null;

function getConnection(): HubConnection {
  if (connection) {
    return connection;
  }

  connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => getAuthToken() ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  return connection;
}

async function ensureStarted(): Promise<HubConnection> {
  const hubConnection = getConnection();
  if (hubConnection.state === HubConnectionState.Connected) {
    return hubConnection;
  }

  if (!startPromise) {
    startPromise = hubConnection
      .start()
      .catch((error) => {
        connection = null;
        throw error;
      })
      .finally(() => {
        startPromise = null;
      });
  }

  await startPromise;
  return hubConnection;
}

export async function subscribeToFileJobUpdates(
  onJobUpdated: (payload: JobUpdatedPayload) => void,
): Promise<() => void> {
  const hubConnection = await ensureStarted();
  const handler = (payload: JobUpdatedPayload) => onJobUpdated(payload);
  hubConnection.on("jobUpdated", handler);

  return () => {
    hubConnection.off("jobUpdated", handler);
  };
}
