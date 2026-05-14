# SignalR Runtime Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build live frontend updates for runtime telemetry, tag values, emulator metrics, and events through SignalR.

**Architecture:** Add one typed SignalR hub and a `RuntimeUpdateService` that becomes the only backend publication point for live data. Runtime jobs and feature services publish typed DTOs through that service, while the frontend connects once and merges incoming updates into Zustand state.

**Tech Stack:** ASP.NET Core SignalR, Autofac, Quartz, xUnit, React/Vite, Zustand, `@microsoft/signalr`.

---

### Task 1: Backend Update Service

**Files:**
- Create: `UniEmu/Realtime/RuntimeUpdatesHub.cs`
- Create: `UniEmu/Realtime/RuntimeUpdateDtos.cs`
- Create: `UniEmu/Realtime/RuntimeUpdateService.cs`
- Test: `UniEmu.Tests/Realtime/RuntimeUpdateServiceTests.cs`

- [ ] Write a failing xUnit test proving telemetry publication sends updates to `runtime:all` and `emulator:{id}`.
- [ ] Run `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter RuntimeUpdateServiceTests` and verify it fails because realtime types do not exist.
- [ ] Implement DTOs, broadcaster abstraction, and `RuntimeUpdateService`.
- [ ] Run the same test and verify it passes.

### Task 2: SignalR Wiring

**Files:**
- Modify: `UniEmu/Program.cs`
- Modify: `UniEmu/Realtime/RuntimeUpdatesHub.cs`
- Modify: `UniEmu/Realtime/RuntimeUpdateService.cs`

- [ ] Register SignalR and map `/hubs/runtime-updates`.
- [ ] Register `RuntimeUpdateService` and the SignalR broadcaster in Autofac.
- [ ] Add hub methods `SubscribeAll`, `SubscribeEmulator`, and `UnsubscribeEmulator`.
- [ ] Run backend tests.

### Task 3: Runtime Publishers

**Files:**
- Modify: `UniEmu/Runtime/TagValueJob.cs`
- Modify: `UniEmu/Runtime/EmulatorPublishJob.cs`
- Modify: `UniEmu/Features/Telemetry/TelemetryService.cs`

- [ ] Inject `RuntimeUpdateService`.
- [ ] Publish tag values after successful tag generation.
- [ ] Publish telemetry, emulator DTO, and event DTO after publish job save.
- [ ] Publish ingested telemetry after `TelemetryService.IngestAsync`.
- [ ] Run backend tests.

### Task 4: Frontend SignalR Client

**Files:**
- Modify: `UniEmu.Client/package.json`
- Modify: `UniEmu.Client/vite.config.ts`
- Create: `UniEmu.Client/src/realtime/runtime-updates-client.ts`
- Modify: `UniEmu.Client/src/types/uniemu.ts`
- Modify: `UniEmu.Client/src/store/uniemu-store.ts`

- [ ] Add `@microsoft/signalr`.
- [ ] Proxy `/hubs/runtime-updates` with WebSocket support in Vite.
- [ ] Create a client wrapper with automatic reconnect and typed event callbacks.
- [ ] Add Zustand actions for connecting, disconnecting, and emulator subscriptions.
- [ ] Merge incoming telemetry, tag, emulator, and event updates into existing store state.
- [ ] Run frontend build.

### Task 5: App Integration

**Files:**
- Modify: `UniEmu.Client/src/routes/__root.tsx`
- Modify: `UniEmu.Client/src/routes/emulators/$id.tsx`

- [ ] Connect realtime after app hydration.
- [ ] Subscribe/unsubscribe emulator-specific updates on detail page mount/unmount.
- [ ] Run backend tests and frontend build.
