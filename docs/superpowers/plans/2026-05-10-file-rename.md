# File Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add inline renaming for existing script files and CNC programs.

**Architecture:** Reuse existing backend PATCH support and frontend store actions. Add rename UI to the existing `FileRow` components and pass handlers from their pages.

**Tech Stack:** React, TypeScript, Zustand store actions, Node source tests.

---

### Task 1: Tests

**Files:**
- Create: `UniEmu.Client/src/routes/scripts/components/FileRow.rename.test.mjs`
- Create: `UniEmu.Client/src/routes/cnc/components/FileRow.rename.test.mjs`

- [x] **Step 1: Write failing tests**

The tests assert that each file row has `onRename`, inline editing state, Enter/Escape handling, a trimmed save, and a pencil button.

- [x] **Step 2: Run tests and confirm RED**

Run:

```bash
node src/routes/scripts/components/FileRow.rename.test.mjs
node src/routes/cnc/components/FileRow.rename.test.mjs
```

Expected: both fail because `onRename` is missing.

### Task 2: Script Rename UI

**Files:**
- Modify: `UniEmu.Client/src/routes/scripts/components/FileRow.tsx`
- Modify: `UniEmu.Client/src/routes/scripts/components/ScriptsPage.tsx`

- [ ] **Step 1: Add inline rename mode to script `FileRow`**

Add `onRename`, local `draftName`, local `editingName`, Enter save, Escape cancel, and a `Pencil` icon button.

- [ ] **Step 2: Wire page handler**

Read `renameScript` from the store and pass `onRename={(name) => void renameScript(sc.id, name)}` for shared and emulator script rows.

### Task 3: CNC Rename UI

**Files:**
- Modify: `UniEmu.Client/src/routes/cnc/components/FileRow.tsx`
- Modify: `UniEmu.Client/src/routes/cnc/components/CncStoragePage.tsx`

- [ ] **Step 1: Add inline rename mode to CNC `FileRow`**

Add the same compact rename behavior while preserving download, delete, and file size display.

- [ ] **Step 2: Wire page handler**

Pass `onRename={(name) => void updateCncProgram(p.id, { name })}` for shared and emulator CNC rows.

### Task 4: Verification

- [ ] **Step 1: Run focused tests**

```bash
node src/routes/scripts/components/FileRow.rename.test.mjs
node src/routes/cnc/components/FileRow.rename.test.mjs
```

Expected: both pass.

- [ ] **Step 2: Run frontend build**

```bash
yarn build
```

Expected: production build completes.
