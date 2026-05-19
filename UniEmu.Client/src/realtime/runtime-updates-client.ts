import * as signalR from '@microsoft/signalr';
import type {
  Emulator,
  RuntimeTagValueUpdate,
  RuntimeTelemetryUpdate,
  SystemEvent,
} from '@/types/uniemu';
import { API_BASE_URL } from '@/lib/constants';

type RuntimeUpdateHandlers = {
  onTelemetryPoint: (update: RuntimeTelemetryUpdate) => void;
  onTagValue: (update: RuntimeTagValueUpdate) => void;
  onEmulatorUpdated: (emulator: Emulator) => void;
  onEventCreated: (event: SystemEvent) => void;
  onConnectionStateChanged?: (connected: boolean) => void;
};

export class RuntimeUpdatesClient {
  private readonly connection: signalR.HubConnection;
  private startPromise: Promise<void> | null = null;

  constructor(private readonly handlers: RuntimeUpdateHandlers) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(getRuntimeUpdatesHubUrl())
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('TelemetryPoint', handlers.onTelemetryPoint);
    this.connection.on('TagValue', handlers.onTagValue);
    this.connection.on('EmulatorUpdated', handlers.onEmulatorUpdated);
    this.connection.on('EventCreated', handlers.onEventCreated);
    this.connection.onreconnecting(() => handlers.onConnectionStateChanged?.(false));
    this.connection.onreconnected(() => {
      handlers.onConnectionStateChanged?.(true);
      void this.connection.invoke('SubscribeAll');
    });
    this.connection.onclose(() => {
      this.startPromise = null;
      handlers.onConnectionStateChanged?.(false);
    });
  }

  start(): Promise<void> {
    if (this.connection.state === signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }

    this.startPromise ??= this.connection
      .start()
      .then(() => {
        this.handlers.onConnectionStateChanged?.(true);
        return this.connection.invoke('SubscribeAll');
      })
      .catch((error) => {
        this.startPromise = null;
        throw error;
      });

    return this.startPromise;
  }

  async stop(): Promise<void> {
    this.startPromise = null;
    await this.connection.stop();
  }

  async subscribeEmulator(emulatorId: string): Promise<void> {
    await this.start();
    await this.connection.invoke('SubscribeEmulator', emulatorId);
  }

  async unsubscribeEmulator(emulatorId: string): Promise<void> {
    if (this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('UnsubscribeEmulator', emulatorId);
  }
}

function getRuntimeUpdatesHubUrl(): string {
  if (!API_BASE_URL) {
    return '/hubs/runtime-updates';
  }

  return `${API_BASE_URL.replace(/\/+$/, '')}/hubs/runtime-updates`;
}
