# SignalR Runtime Updates Design

## Goal

Add real-time frontend updates for telemetry, generated tag values, emulator metrics, events, and future mutable runtime data through a single backend publication service.

## Architecture

The backend exposes one SignalR hub at `/hubs/runtime-updates`. Runtime jobs and feature services do not talk to SignalR directly; they call `RuntimeUpdateService`, which is the single publication point for live changes. The service publishes typed update DTOs to SignalR groups so the frontend can receive global updates and emulator-specific updates.

REST remains the source of initial snapshots and fallback data. SignalR carries incremental changes after hydration.

## Backend Components

- `RuntimeUpdatesHub` accepts client subscription calls.
- `RuntimeUpdateService` exposes publication methods for telemetry, tag values, emulator updates, and system events.
- `SignalRRuntimeUpdateBroadcaster` adapts the service to `IHubContext<RuntimeUpdatesHub, IRuntimeUpdatesClient>`.
- `IRuntimeUpdatesClient` defines client methods:
  - `TelemetryPoint(RuntimeTelemetryUpdateDto update)`
  - `TagValue(RuntimeTagValueUpdateDto update)`
  - `EmulatorUpdated(EmulatorDto emulator)`
  - `EventCreated(SystemEventDto ev)`

Groups:

- `runtime:all` receives global updates.
- `emulator:{id}` receives updates for one emulator.

Clients join `runtime:all` on connect and can call `SubscribeEmulator(emulatorId)` / `UnsubscribeEmulator(emulatorId)` for focused pages.

## Update Sources

- `EmulatorPublishJob` publishes telemetry, emulator metrics, and created event after a publish attempt is saved.
- `TagValueJob` publishes generated tag values after `TagRuntimeStateStore.Set`.
- `TelemetryService.IngestAsync` publishes ingested telemetry after saving.
- Existing CRUD/status actions continue returning updated REST DTOs; later they can call the same service for broader live CRUD refreshes.

## Frontend Components

- Add `@microsoft/signalr`.
- Add a small realtime client module that builds the hub URL from the existing API base URL.
- Zustand owns the connection lifecycle through store actions:
  - `connectRealtime`
  - `disconnectRealtime`
  - `subscribeRealtimeEmulator`
  - `unsubscribeRealtimeEmulator`
- Incoming updates merge into the existing state:
  - telemetry points are appended and trimmed by `packetRetention`;
  - tag values update tag `preview`;
  - emulator updates are upserted;
  - events are prepended and capped at 200.

## Error Handling

The SignalR client uses automatic reconnect. If the connection drops, existing REST state remains visible. On reconnect, the store can re-run `hydrate` or detail loading later; the first slice only restores subscriptions and accepts new updates.

## Testing

Backend tests cover the publication service without hosting SignalR by using an in-memory broadcaster. Store-level frontend tests are not currently configured in the repo, so frontend verification is build/typecheck based.
