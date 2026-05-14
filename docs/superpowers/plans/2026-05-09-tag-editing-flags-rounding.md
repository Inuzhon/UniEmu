# Tag Editing Flags And Rounding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist tag dispatch enablement, warn before losing drawer edits, and round double tag values consistently after calculation.

**Architecture:** Extend the existing tag entity/DTO/request contract with `Enabled` and `RoundDigits`. Runtime still computes every tag and stores/publishes its calculated value, but Dispatcher payload filters out disabled tags. Rounding is applied immediately after tag calculation/casting so Dispatcher, telemetry, realtime updates, and previews agree.

**Tech Stack:** .NET 10, EF Core SQLite, Quartz runtime jobs, xUnit, React/Vite, Radix Sheet/AlertDialog, Zustand.

---

### Task 1: Backend tag contract and runtime behavior

**Files:**
- Modify: `UniEmu/Domain/Entities/EmulatorTagEntity.cs`
- Modify: `UniEmu/Contracts/Dtos/UniEmuDtos.cs`
- Modify: `UniEmu/Contracts/Requests/UniEmuRequests.cs`
- Modify: `UniEmu/Mapping/UniEmuMapping.cs`
- Modify: `UniEmu/Data/UniEmuDbContext.cs`
- Modify: `UniEmu/Data/UniEmuSchemaUpdater.cs`
- Modify: `UniEmu/Features/Tags/TagService.cs`
- Modify: `UniEmu/Runtime/TelemetryValueGenerator.cs`
- Modify: `UniEmu/Runtime/TagScriptExecutionService.cs`
- Modify: `UniEmu/Runtime/EmulatorPublishJob.cs`
- Test: `UniEmu.Tests/Runtime/TelemetryValueGeneratorTests.cs`
- Test: `UniEmu.Tests/Runtime/EmulatorPublishJobTests.cs`

- [ ] Write failing tests for double rounding and dispatcher enabled filtering.
- [ ] Run targeted tests and confirm they fail for missing behavior.
- [ ] Add `Enabled` and `RoundDigits` to backend contract/storage with compatibility defaults.
- [ ] Apply rounding after generation/casting for generator/static/scenario/script/formula values.
- [ ] Filter Dispatcher `UniversalValue` list to enabled tags only while keeping telemetry/runtime values from all generated tags.
- [ ] Run targeted tests and confirm they pass.

### Task 2: Frontend tag editor behavior

**Files:**
- Modify: `UniEmu.Client/src/types/uniemu.ts`
- Modify: `UniEmu.Client/src/components/AddTagDrawer.tsx`

- [ ] Add `roundDigits?: number | null` to `EmulatorTag`.
- [ ] Hydrate/reset draft state for enabled and rounding controls.
- [ ] Show double-only “Округлять до” switch and digits input.
- [ ] Build payload with `roundDigits` set to a number when enabled, otherwise `null`.
- [ ] Add dirty-state detection and unsaved-close confirmation.
- [ ] Prevent ESC close in edit mode.

### Task 3: Frontend emulator drawer warning

**Files:**
- Modify: `UniEmu.Client/src/components/EditEmulatorDrawer.tsx`

- [ ] Add draft snapshot comparison for emulator fields.
- [ ] Route close attempts through an unsaved-change confirmation dialog.
- [ ] Keep save behavior unchanged.

### Task 4: Verification

**Files:**
- Check: backend and frontend projects

- [ ] Run `dotnet test UniEmu.Tests/UniEmu.Tests.csproj`.
- [ ] Run frontend verification with `npm run build` from `UniEmu.Client` if dependencies are present.
- [ ] Report any existing unrelated dirty files separately.
