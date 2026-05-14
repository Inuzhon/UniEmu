# Frontend Feature By Route Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the React frontend so every route has its own folder and route-local UI lives in a route `components` folder, without changing runtime behavior.

**Architecture:** Keep TanStack Router file-based routing as the source of truth. Use route folders for top-level pages, a pathless `(dashboard)` route group for `/`, and keep globally reused primitives in `src/components`.

**Tech Stack:** React, Vite, TanStack Router, TypeScript, Tailwind CSS.

---

### Task 1: Move Routes Into Folders

**Files:**
- Move: `UniEmu.Client/src/routes/index.tsx` -> `UniEmu.Client/src/routes/(dashboard)/index.tsx`
- Move: `UniEmu.Client/src/routes/scripts.tsx` -> `UniEmu.Client/src/routes/scripts/index.tsx`
- Move: `UniEmu.Client/src/routes/cnc.tsx` -> `UniEmu.Client/src/routes/cnc/index.tsx`
- Move: `UniEmu.Client/src/routes/settings.tsx` -> `UniEmu.Client/src/routes/settings/index.tsx`
- Move: `UniEmu.Client/src/routes/logs.tsx` -> `UniEmu.Client/src/routes/logs/index.tsx`

- [ ] Create route folders.
- [ ] Move route files.
- [ ] Update `createFileRoute` literals where index routes need trailing slash ids.
- [ ] Run `npm run build` to let TanStack regenerate `routeTree.gen.ts`.

### Task 2: Extract Route-Local Components

**Files:**
- Create: `UniEmu.Client/src/routes/(dashboard)/components/*`
- Create: `UniEmu.Client/src/routes/scripts/components/*`
- Create: `UniEmu.Client/src/routes/cnc/components/*`
- Create: `UniEmu.Client/src/routes/settings/components/*`
- Create: `UniEmu.Client/src/routes/emulators/components/*`
- Modify imports in route files.

- [ ] Move inline dashboard tiles into dashboard components.
- [ ] Move scripts tree/editor/modal components into scripts components.
- [ ] Move CNC tree/viewer/drop-zone components into CNC components.
- [ ] Move settings telemetry card into settings components.
- [ ] Move emulator drawers and tag scenario UI into emulators components.

### Task 3: Keep Shared Components Clean

**Files:**
- Keep: `UniEmu.Client/src/components/ui/*`
- Keep: `UniEmu.Client/src/components/Layout/*`
- Keep: `UniEmu.Client/src/components/StatusBadge.tsx`
- Keep: `UniEmu.Client/src/components/TimeAgo.tsx`
- Keep: `UniEmu.Client/src/components/PagePlaceholder.tsx`
- Keep shared CSX editor temporarily in `src/components` because it is reused by scripts and emulator tag formula UI.

- [ ] Remove route-specific imports from shared component locations.
- [ ] Do not move reusable UI primitives.

### Task 4: Verify No Behavior Change

- [ ] Run `npm run build`.
- [ ] Run `npm run lint` if build is clean.
- [ ] Review `git diff --stat` and generated route tree for expected route paths.
