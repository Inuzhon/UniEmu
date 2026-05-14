# Tag runtime cache and deferred preview flush design

## Context

`TagValueJob` currently performs a tracked database read on every tag tick:
it loads the tag, the owning emulator, and all emulator tags, then writes the
computed value back to `EmulatorTags.Preview`. For short intervals this makes
both reads and writes part of the hot path even though tag configuration changes
rarely.

The runtime already has two useful building blocks:

- `CachedUniEmuDataService` caches emulator snapshots and is invalidated by tag,
  emulator, script, and CNC program mutations.
- `TagRuntimeStateStore` stores the newest in-memory tag values used by realtime
  updates and script tag access.

The optimization should keep runtime values fresh while turning database
`Preview` updates into durable snapshots rather than per-tick persistence.

## Goals

- Remove per-tick configuration reads from `TagValueJob`.
- Remove per-tick `Preview` writes from the normal tag execution path.
- Preserve current generator, formula, script, rounding, static tag side effect,
  realtime update, and persisted preview behavior.
- Keep cache invalidation explicit when tag or emulator configuration changes.
- Keep shutdown persistence as a final safety net.

## Non-goals

- Changing public API contracts.
- Changing trigger scheduling semantics.
- Persisting every intermediate generated value to the database.
- Replacing `TagRuntimeStateStore` with a new runtime-state system.

## Architecture

`TagValueJob` should use `CachedUniEmuDataService.GetEmulatorWithTagsAsync` to
load the emulator snapshot. It should find the requested tag in the cached
snapshot, verify that the emulator is still running, calculate the value, update
`TagRuntimeStateStore`, mark the tag preview as dirty for deferred persistence,
and publish the realtime update.

A new runtime service, tentatively `TagPreviewFlushService`, owns deferred
database writes. `TagValueJob` marks dirty values after successful calculation.
The flush service writes dirty tag previews in batches using a scoped
`UniEmuDbContext`. Existing shutdown persistence through
`TagRuntimeStatePersistenceService.PersistToTagPreviewsAsync` remains a final
fallback and may reuse the same dirty-value path if that keeps the design
simpler.

`TagScriptExecutionService` should continue to read current values from
`TagRuntimeStateStore` first and fall back to cached `Preview` values only when a
runtime value is not available. This keeps script behavior fresh even when
database previews lag behind the runtime by a few seconds.

## Data Flow

1. A Quartz tag trigger invokes `TagValueJob`.
2. `TagValueJob` gets the cached emulator snapshot and tag configuration.
3. If the emulator is missing, stopped, or the tag no longer exists, the job
   clears that tag from `TagRuntimeStateStore` and exits.
4. The job computes the tag value using the existing generator or script
   execution service.
5. The job stores the current value in `TagRuntimeStateStore`.
6. The job marks the tag preview dirty for deferred persistence.
7. The job publishes `RuntimeTagValueUpdateDto`.
8. `TagPreviewFlushService` periodically saves dirty previews to
   `EmulatorTags.Preview`.
9. Application shutdown persists the latest runtime snapshot as a final durable
   checkpoint.

## Invalidation

Configuration cache invalidation stays explicit and mutation-driven:

- tag create, replace, and delete call `dataCache.InvalidateEmulator(emulatorId)`;
- emulator changes call `dataCache.InvalidateEmulator(emulatorId)`;
- script changes call `dataCache.InvalidateScripts()`;
- CNC program changes call `dataCache.InvalidateCncPrograms()`.

Deferred preview flush must not invalidate the emulator configuration cache.
`Preview` writes are runtime snapshots, not configuration changes, and
invalidating on each flush would reintroduce avoidable reads.

After tag or emulator configuration changes, the existing reschedule path
continues to delete old Quartz jobs and schedule new ones from the refreshed
configuration snapshot.

## Persistence Semantics

The database `Preview` field becomes eventually consistent during runtime. It is
expected to lag behind the in-memory value until the next flush or shutdown
checkpoint. The authoritative live value is `TagRuntimeStateStore`.

Recommended flush behavior:

- coalesce dirty values by `(emulatorId, tagId)`;
- write only the latest preview for each tag;
- flush periodically, for example every 1-5 seconds;
- flush when an emulator is stopped or unscheduled;
- keep shutdown persistence to reduce data loss if the process exits normally.

If a flush races with tag deletion, the service should skip missing rows. If it
races with tag replacement, it should only update `Preview`, leaving
configuration columns untouched.

## Error Handling

Value-generation failures keep the existing behavior of logging a warning and
creating a `SystemEventEntity`. Because errors are exceptional and already write
events, they may still call `SaveChangesAsync` immediately.

Flush failures should log a warning and keep dirty values queued for a later
attempt unless cancellation is requested. A normal runtime calculation should
not fail only because deferred preview persistence failed.

## Testing

Add or update backend tests for these behaviors:

- repeated `TagValueJob` execution uses cached emulator/tag configuration and
  avoids repeated configuration reads;
- successful tag execution updates `TagRuntimeStateStore` and publishes realtime
  updates without requiring an immediate `Preview` write;
- deferred flush writes the latest dirty preview to the database;
- dirty values are coalesced so multiple calculations persist only the latest
  value;
- script tags still see the latest values through `TagRuntimeStateStore`;
- static tag side effects from scripts are persisted by the deferred flush;
- cache invalidation on tag replacement causes subsequent executions to use the
  new tag configuration;
- missing/stopped/deleted tags clear runtime state and do not write stale
  previews.

## Rollout

Implement in small steps:

1. Add tests around the desired no-repeat-read and deferred-write behavior.
2. Introduce the dirty preview flush service and registration.
3. Switch `TagValueJob` to cached snapshots and dirty marking.
4. Add periodic or lifecycle flush integration.
5. Run focused runtime tests, then the backend test project.
