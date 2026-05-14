# File Rename Design

## Goal

Allow users to rename already loaded script files and CNC programs from the existing file trees.

## Design

Both `scripts` and `cnc` file rows get an inline rename action next to existing row actions. Clicking the pencil switches the filename area into a compact input. Pressing Enter saves the trimmed name, Escape cancels and restores the current name, and empty or unchanged values are ignored.

Scripts use the existing `renameScript(id, name)` store action, which calls `PATCH /api/scripts/{id}`. CNC programs use the existing `updateCncProgram(id, { name })` action, which calls `PATCH /api/cnc-programs/{id}`. No backend endpoint changes are required.

## Testing

Add focused source tests for both file row components to lock in the rename prop, inline editing state, Enter/Escape handling, and pencil action.
