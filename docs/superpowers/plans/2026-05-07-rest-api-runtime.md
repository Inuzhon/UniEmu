# UniEmu REST API Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first backend slice for UniEmu Hub: persistent REST API, EF Core SQLite storage, and a background emulator runtime.

**Architecture:** Keep one ASP.NET Core Web API project and split code by feature folders. Controllers stay thin, feature services own EF queries and business rules, runtime runs as a hosted service and uses scoped services/DbContext per tick.

**Tech Stack:** ASP.NET Core `net10.0`, EF Core SQLite, controllers, hosted services, xUnit integration tests where project setup allows.

---

## File Structure

- Modify `UniEmu/UniEmu.csproj`: add EF Core SQLite and testable infrastructure dependencies.
- Modify `UniEmu/appsettings.json`: add SQLite connection string.
- Modify `UniEmu/Program.cs`: register JSON enum handling, EF Core, feature services, runtime services, controllers.
- Remove template files `UniEmu/WeatherForecast.cs` and `UniEmu/Controllers/WeatherForecastController.cs`.
- Create `UniEmu/Data/UniEmuDbContext.cs`: DbSets and model configuration.
- Create `UniEmu/Data/Entities/*.cs`: EF entities.
- Create `UniEmu/Features/*/*.cs`: DTOs, controllers, services for the API contract.
- Create `UniEmu/Runtime/*.cs`: hosted runtime, value generator, target sender.
- Create `UniEmu/Common/*.cs`: shared helpers for clock, JSON, IDs if needed.
- Optionally create `UniEmu.Tests/UniEmu.Tests.csproj`: API/runtime tests if local packages can be restored.

## Task 1: Backend Foundation

**Files:**
- Modify: `UniEmu/UniEmu.csproj`
- Modify: `UniEmu/appsettings.json`
- Modify: `UniEmu/Program.cs`
- Create: `UniEmu/Data/UniEmuDbContext.cs`
- Create: `UniEmu/Data/Entities/EmulatorEntity.cs`
- Create: `UniEmu/Data/Entities/EmulatorTagEntity.cs`
- Create: `UniEmu/Data/Entities/ScriptFileEntity.cs`
- Create: `UniEmu/Data/Entities/CncProgramEntity.cs`
- Create: `UniEmu/Data/Entities/TelemetryPointEntity.cs`
- Create: `UniEmu/Data/Entities/SystemEventEntity.cs`

- [ ] Add EF Core SQLite package references.
- [ ] Add `ConnectionStrings:UniEmuDb` with default `Data Source=uniemu.db`.
- [ ] Register `UniEmuDbContext` with `UseSqlite`.
- [ ] Configure JSON options so enums serialize as strings.
- [ ] Define entities with string IDs, UTC timestamps, and JSON string columns for nested tag config.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: project compiles after package restore.

## Task 2: Contracts and Mapping

**Files:**
- Create: `UniEmu/Features/Contracts/UniEmuDtos.cs`
- Create: `UniEmu/Features/Contracts/UniEmuEnums.cs`
- Create: `UniEmu/Features/Contracts/UniEmuMapping.cs`

- [ ] Add DTOs matching `UniEmu.Client/src/types/uniemu.ts` and `backend_endpoints.md`.
- [ ] Keep enum string values compatible with the frontend: `Running`, `Stopped`, `int`, `shared`, `info`, etc.
- [ ] Add mapping methods from EF entities to DTOs.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: DTO and mapping code compiles.

## Task 3: Emulators and Tags API

**Files:**
- Create: `UniEmu/Features/Emulators/EmulatorsController.cs`
- Create: `UniEmu/Features/Emulators/EmulatorService.cs`
- Create: `UniEmu/Features/Tags/TagsController.cs`
- Create: `UniEmu/Features/Tags/TagService.cs`

- [ ] Implement `GET /api/emulators`.
- [ ] Implement `GET /api/emulators/{emulatorId}`.
- [ ] Implement `POST /api/emulators`.
- [ ] Implement `PATCH /api/emulators/{emulatorId}`.
- [ ] Implement `PATCH /api/emulators/{emulatorId}/status`.
- [ ] Implement tag list/create/update/delete under `/api/emulators/{emulatorId}/tags`.
- [ ] Return `ProblemDetails`-compatible `404`/`400` responses through controller helpers.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: API compiles.

## Task 4: Scripts, CNC Programs, Events, Telemetry API

**Files:**
- Create: `UniEmu/Features/Scripts/ScriptsController.cs`
- Create: `UniEmu/Features/Scripts/ScriptService.cs`
- Create: `UniEmu/Features/CncPrograms/CncProgramsController.cs`
- Create: `UniEmu/Features/CncPrograms/CncProgramService.cs`
- Create: `UniEmu/Features/Events/EventsController.cs`
- Create: `UniEmu/Features/Events/EventService.cs`
- Create: `UniEmu/Features/Telemetry/TelemetryController.cs`
- Create: `UniEmu/Features/Telemetry/TelemetryService.cs`

- [ ] Implement scripts CRUD with `scope` and `emulatorId` validation.
- [ ] Implement CNC list/upload/update/delete endpoints.
- [ ] Implement events list with `cursor` and `limit`; use timestamp/id cursor if needed.
- [ ] Implement `POST /api/events`.
- [ ] Implement `GET /api/emulators/{emulatorId}/telemetry?points={n}`.
- [ ] Implement `POST /api/telemetry/ingest`.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: API compiles.

## Task 5: Runtime

**Files:**
- Create: `UniEmu/Runtime/EmulatorRuntimeService.cs`
- Create: `UniEmu/Runtime/TelemetryValueGenerator.cs`
- Create: `UniEmu/Runtime/TelemetryPacketSender.cs`
- Modify: `UniEmu/Program.cs`

- [ ] Register `EmulatorRuntimeService` as hosted service.
- [ ] Register named/typed `HttpClient` for telemetry target POSTs.
- [ ] Implement runtime loop with cancellation support and one scoped DbContext per iteration.
- [ ] Generate values for `static`, `generator`, and `scenario`.
- [ ] Publish `Preview` for `script`, `formula`, and `cnc`.
- [ ] Persist telemetry and system events.
- [ ] Update emulator `LastRun`, `NextRun`, `TotalRequests`, `LastError`.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: runtime compiles.

## Task 6: Seed and Startup Database Creation

**Files:**
- Create: `UniEmu/Data/UniEmuSeeder.cs`
- Modify: `UniEmu/Program.cs`

- [ ] Seed representative emulators, tags, scripts, CNC programs and events based on the current frontend seed data.
- [ ] On development startup, call `Database.EnsureCreatedAsync()` and seed only when database is empty.
- [ ] Keep seed small enough to maintain manually.
- [ ] Run `dotnet build UniEmu/UniEmu.csproj`; expected result: startup code compiles.

## Task 7: Frontend API Adapter

**Files:**
- Create: `UniEmu.Client/src/api/uniemu-api.ts`
- Modify: `UniEmu.Client/src/store/uniemu-store.ts`
- Modify pages using `useUniEmuStore` only where needed for the first integration.

- [ ] Add typed API functions for backend endpoints.
- [ ] Replace initial data loading with backend calls in a minimal way.
- [ ] Keep Zustand as UI/cache layer to limit frontend churn.
- [ ] Run `npm run lint` in `UniEmu.Client`; expected result: no new lint errors.
- [ ] Run `npm run build` in `UniEmu.Client`; expected result: production build succeeds.

## Task 8: Verification

**Files:**
- Modify as needed based on compile/runtime errors.

- [ ] Run `dotnet build UniEmu.slnx`; expected result: solution compiles.
- [ ] Run backend locally and check `/api/emulators` returns seeded data.
- [ ] Start an emulator through `PATCH /api/emulators/{id}/status` and verify telemetry appears.
- [ ] Verify failed target POST records `LastError` and an error event.
- [ ] Run frontend lint/build after API adapter changes.

## Self-Review

- Spec coverage: all REST, SQLite persistence, runtime, telemetry ingest/history, events, and frontend integration are covered by tasks.
- Placeholder scan: no `TBD`/`TODO` steps remain.
- Type consistency: task names and paths match the approved design document.
- Git note: do not commit automatically; commits require an explicit user request.

