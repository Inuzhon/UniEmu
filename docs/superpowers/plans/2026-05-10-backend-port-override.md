# Backend Port Override Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the backend listen port to be overridden from configuration, `.env`, and Docker Compose, and log the selected port on startup.

**Architecture:** Add a small backend options helper that resolves `UniEmu:Port` with default `5083` and creates the Kestrel URL. `Program.cs` uses that helper before building the app host and logs the resolved port before `RunAsync`. Docker Compose passes `UniEmu__Port` from `UNIEMU_PORT`.

**Tech Stack:** .NET 10, ASP.NET Core configuration, xUnit, Docker Compose, Vite env.

---

### Task 1: Backend Port Configuration

**Files:**
- Create: `UniEmu/Hosting/BackendPortOptions.cs`
- Test: `UniEmu.Tests/Hosting/BackendPortOptionsTests.cs`
- Modify: `UniEmu/Program.cs`

- [ ] Write failing tests for default port, configured port, and URL formatting.
- [ ] Run `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter BackendPortOptionsTests` and verify failure because the helper does not exist.
- [ ] Implement `BackendPortOptions` with default port `5083`, config key `UniEmu:Port`, and URL `http://0.0.0.0:{port}`.
- [ ] Update `Program.cs` to call `builder.WebHost.UseUrls(BackendPortOptions.Resolve(builder.Configuration).HttpUrl)` and log `UniEmu backend listening on port {Port}` before `RunAsync`.
- [ ] Re-run the focused test and verify it passes.

### Task 2: Environment And Compose

**Files:**
- Modify: `UniEmu/appsettings.json`
- Modify: `UniEmu.Client/.env`
- Modify: `UniEmu.Client/docker-compose.yml`

- [ ] Add `"Port": 5083` under `UniEmu` in `appsettings.json`.
- [ ] Add `UNIEMU_PORT=5083` to `.env`.
- [ ] Update `docker-compose.yml` with backend service that builds `../UniEmu`, maps `${UNIEMU_PORT:-5083}:${UNIEMU_PORT:-5083}`, and passes `UniEmu__Port=${UNIEMU_PORT:-5083}`.
- [ ] Update frontend compose env so `VITE_API_PROXY_TARGET=http://backend:${UNIEMU_PORT:-5083}` is available in compose.

### Task 3: Verification

**Files:**
- No additional files.

- [ ] Run `dotnet test UniEmu.Tests/UniEmu.Tests.csproj`.
- [ ] If feasible, run `docker compose -f UniEmu.Client/docker-compose.yml config` to validate compose interpolation.
