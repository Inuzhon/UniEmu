# Autofac And Serilog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure UniEmu backend to use Autofac as the service provider and Serilog as the configured logging provider.

**Architecture:** Use host-level integration in `Program.cs`. Keep existing service registrations in `builder.Services` and read all Serilog sink/level/format settings from `appsettings`.

**Tech Stack:** ASP.NET Core, Autofac.Extensions.DependencyInjection, Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File.

---

### Task 1: Add Packages

**Files:**
- Modify: `UniEmu/UniEmu.csproj`

- [x] Add `Autofac.Extensions.DependencyInjection`.
- [x] Add `Serilog.AspNetCore`.
- [x] Add `Serilog.Sinks.Console`.
- [x] Add `Serilog.Sinks.File`.

### Task 2: Wire Host Integrations

**Files:**
- Modify: `UniEmu/Program.cs`

- [x] Add `Autofac.Extensions.DependencyInjection` and `Serilog` usings.
- [x] Call `builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())`.
- [x] Call `builder.Host.UseSerilog(...)` and read logger configuration from `builder.Configuration`.

### Task 3: Replace Logging Configuration

**Files:**
- Modify: `UniEmu/appsettings.json`
- Modify: `UniEmu/appsettings.Development.json`

- [x] Replace the standard `Logging` section with `Serilog`.
- [x] Configure console output template.
- [x] Configure file sink path, daily rolling interval, retained file count, file size limit, and size-based rolling.
- [x] Keep development-specific overrides focused on log levels.

### Task 4: Verify

**Files:**
- Modify: none

- [x] Run:

```powershell
dotnet build UniEmu/UniEmu.csproj --ignore-failed-sources -p:BuildProjectReferences=false
```

- [x] Confirm the project compiles without errors.
- [x] Run a short startup smoke test with `UniEmu__SkipStartupDatabase=true`, `UniEmu__DisableRuntime=true`, and `UniEmu__DisableStaticAssets=true`; confirm the host stays running until manually stopped.
