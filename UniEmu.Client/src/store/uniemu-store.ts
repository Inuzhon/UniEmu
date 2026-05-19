import { create, type StateCreator } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { uniEmuApi } from '@/api/uniemu-api';
import { PERSIST_STORE } from '@/lib/feature-flags';
import { RuntimeUpdatesClient } from '@/realtime/runtime-updates-client';
import type {
  CncProgram,
  CncScope,
  Emulator,
  EmulatorTag,
  RuntimeTagValueUpdate,
  RuntimeTelemetryUpdate,
  ScriptFile,
  ScriptScope,
  SystemEvent,
  TelemetryPoint,
} from '@/types/uniemu';

interface UniEmuState {
  emulators: Emulator[];
  events: SystemEvent[];
  tagsByEmulator: Record<string, EmulatorTag[]>;
  scripts: ScriptFile[];
  cncPrograms: CncProgram[];
  telemetryByEmulator: Record<string, TelemetryPoint[]>;
  online: boolean;
  loading: boolean;
  apiError: string | null;
  packetRetention: number;
  hydrate: () => Promise<void>;
  connectRealtime: () => Promise<void>;
  disconnectRealtime: () => Promise<void>;
  subscribeRealtimeEmulator: (emulatorId: string) => Promise<void>;
  unsubscribeRealtimeEmulator: (emulatorId: string) => Promise<void>;
  loadEmulatorDetails: (emulatorId: string) => Promise<void>;
  setPacketRetention: (n: number) => void;
  toggleStatus: (id: string) => Promise<void>;
  createEmulator: (input: { name: string; targetUrl: string; intervalSec: number; protocolId: number }) => Promise<string>;
  updateEmulator: (
    id: string,
    patch: Partial<Pick<Emulator, 'name' | 'targetUrl' | 'intervalSec' | 'protocolId'>>,
  ) => Promise<void>;
  downloadDispatcherTemplate: (emulatorId: string) => Promise<void>;
  deleteEmulator: (id: string) => Promise<void>;
  pushEvent: (ev: SystemEvent) => Promise<void>;
  getTelemetry: (emulatorId: string, points?: number) => TelemetryPoint[];
  refreshTelemetry: (emulatorId: string, points?: number) => Promise<void>;
  addTag: (emulatorId: string, tag: Omit<EmulatorTag, 'id'>) => Promise<string>;
  updateTag: (emulatorId: string, tagId: string, patch: Omit<EmulatorTag, 'id'>) => Promise<void>;
  deleteTag: (emulatorId: string, tagId: string) => Promise<void>;
  updateScript: (id: string, content: string) => Promise<void>;
  createScript: (input: { name: string; scope: ScriptScope; emulatorId?: string }) => Promise<string>;
  deleteScript: (id: string) => Promise<void>;
  renameScript: (id: string, name: string) => Promise<void>;
  uploadCncProgram: (input: {
    name: string;
    scope: CncScope;
    emulatorId?: string;
    content: string;
    sizeBytes: number;
    isBinary?: boolean;
    description?: string;
  }) => Promise<string>;
  updateCncProgram: (
    id: string,
    patch: Partial<Pick<CncProgram, 'content' | 'description' | 'name'>>,
  ) => Promise<void>;
  deleteCncProgram: (id: string) => Promise<void>;
}

let runtimeUpdatesClient: RuntimeUpdatesClient | null = null;

const stateCreator: StateCreator<UniEmuState> = (set, get) => ({
  emulators: [],
  events: [],
  tagsByEmulator: {},
  scripts: [],
  cncPrograms: [],
  telemetryByEmulator: {},
  online: true,
  loading: false,
  apiError: null,
  packetRetention: 50,
  hydrate: async () => {
    set({ loading: true, apiError: null });
    try {
      const [emulators, events, scripts, cncPrograms] = await Promise.all([
        uniEmuApi.emulators.list(),
        uniEmuApi.events.list(200),
        uniEmuApi.scripts.list(),
        uniEmuApi.cncPrograms.list(),
      ]);
      const tagEntries = await Promise.all(
        emulators.map(async (emulator) => [emulator.id, await uniEmuApi.tags.list(emulator.id)] as const),
      );
      set({
        emulators: sortEmulators(emulators),
        events,
        scripts,
        cncPrograms,
        tagsByEmulator: Object.fromEntries(tagEntries),
        online: true,
        loading: false,
      });
    } catch (error) {
      set({
        online: false,
        loading: false,
        apiError: error instanceof Error ? error.message : 'Backend API недоступен',
      });
    }
  },
  connectRealtime: async () => {
    if (typeof window === 'undefined') return;

    runtimeUpdatesClient ??= new RuntimeUpdatesClient({
      onTelemetryPoint: (update) => applyTelemetryUpdate(set, get, update),
      onTagValue: (update) => applyTagValueUpdate(set, update),
      onEmulatorUpdated: (emulator) => {
        set((s) => ({
          emulators: upsertEmulator(s.emulators, emulator),
          online: true,
          apiError: null,
        }));
      },
      onEventCreated: (event) => {
        set((s) => ({
          events: upsertEvent([event, ...s.events]).slice(0, 200),
          online: true,
          apiError: null,
        }));
      },
      onConnectionStateChanged: (connected) => set({ online: connected }),
    });

    try {
      await runtimeUpdatesClient.start();
      set({ online: true, apiError: null });
    } catch (error) {
      set({
        online: false,
        apiError: error instanceof Error ? error.message : 'SignalR realtime недоступен',
      });
    }
  },
  disconnectRealtime: async () => {
    await runtimeUpdatesClient?.stop();
    runtimeUpdatesClient = null;
  },
  subscribeRealtimeEmulator: async (emulatorId) => {
    await get().connectRealtime();
    await runtimeUpdatesClient?.subscribeEmulator(emulatorId);
  },
  unsubscribeRealtimeEmulator: async (emulatorId) => {
    await runtimeUpdatesClient?.unsubscribeEmulator(emulatorId);
  },
  loadEmulatorDetails: async (emulatorId) => {
    try {
      const [emulator, tags, telemetry] = await Promise.all([
        uniEmuApi.emulators.get(emulatorId),
        uniEmuApi.tags.list(emulatorId),
        uniEmuApi.telemetry.list(emulatorId, 60),
      ]);
      set((s) => ({
        emulators: upsertEmulator(s.emulators, emulator),
        tagsByEmulator: { ...s.tagsByEmulator, [emulatorId]: tags },
        telemetryByEmulator: { ...s.telemetryByEmulator, [emulatorId]: telemetry },
        online: true,
        apiError: null,
      }));
    } catch (error) {
      set({ online: false, apiError: error instanceof Error ? error.message : 'Backend API недоступен' });
    }
  },
  setPacketRetention: (n) => set({ packetRetention: Math.max(1, Math.min(1000, Math.round(n))) }),
  toggleStatus: async (id) => {
    const current = get().emulators.find((e) => e.id === id);
    if (!current) return;
    const emulator = await uniEmuApi.emulators.setStatus(id, current.status === 'Running' ? 'Stopped' : 'Running');
    set((s) => ({ emulators: upsertEmulator(s.emulators, emulator), online: true, apiError: null }));
  },
  createEmulator: async (input) => {
    const emulator = await uniEmuApi.emulators.create(input);
    set((s) => ({
      emulators: upsertEmulator(s.emulators, emulator),
      tagsByEmulator: { ...s.tagsByEmulator, [emulator.id]: [] },
      online: true,
      apiError: null,
    }));
    return emulator.id;
  },
  updateEmulator: async (id, patch) => {
    const emulator = await uniEmuApi.emulators.patch(id, patch);
    set((s) => ({ emulators: upsertEmulator(s.emulators, emulator), online: true, apiError: null }));
  },
  downloadDispatcherTemplate: async (emulatorId) => {
    try {
      const { blob, fileName } = await uniEmuApi.emulators.downloadDispatcherTemplate(emulatorId);
      downloadBlob(blob, fileName);
      set({ online: true, apiError: null });
    } catch (error) {
      set({ online: false, apiError: error instanceof Error ? error.message : 'Backend API недоступен' });
      throw error;
    }
  },
  deleteEmulator: async (id) => {
    await uniEmuApi.emulators.delete(id);
    set((s) => {
      const { [id]: _t, ...restTags } = s.tagsByEmulator;
      const { [id]: _tel, ...restTelemetry } = s.telemetryByEmulator;
      void _t; void _tel;
      return {
        emulators: s.emulators.filter((e) => e.id !== id),
        tagsByEmulator: restTags,
        telemetryByEmulator: restTelemetry,
        events: s.events.filter((ev) => ev.emulatorId !== id),
        online: true,
        apiError: null,
      };
    });
  },
  pushEvent: async (ev) => {
    const created = await uniEmuApi.events.create(ev);
    set((s) => ({ events: [created, ...s.events].slice(0, 200) }));
  },
  getTelemetry: (emulatorId, points = 60) => {
    return (get().telemetryByEmulator[emulatorId] ?? []).slice(-points);
  },
  refreshTelemetry: async (emulatorId, points = 60) => {
    const telemetry = await uniEmuApi.telemetry.list(emulatorId, points);
    set((s) => ({ telemetryByEmulator: { ...s.telemetryByEmulator, [emulatorId]: telemetry } }));
  },
  addTag: async (emulatorId, tag) => {
    const created = await uniEmuApi.tags.create(emulatorId, tag);
    set((s) => {
      const list = [...(s.tagsByEmulator[emulatorId] ?? []), created];
      return {
        tagsByEmulator: { ...s.tagsByEmulator, [emulatorId]: list },
        emulators: s.emulators.map((e) => (e.id === emulatorId ? { ...e, tagsCount: list.length } : e)),
      };
    });
    return created.id;
  },
  updateTag: async (emulatorId, tagId, patch) => {
    const updated = await uniEmuApi.tags.replace(emulatorId, tagId, patch);
    set((s) => ({
      tagsByEmulator: {
        ...s.tagsByEmulator,
        [emulatorId]: (s.tagsByEmulator[emulatorId] ?? []).map((t) => (t.id === tagId ? updated : t)),
      },
    }));
  },
  deleteTag: async (emulatorId, tagId) => {
    await uniEmuApi.tags.delete(emulatorId, tagId);
    set((s) => {
      const list = (s.tagsByEmulator[emulatorId] ?? []).filter((t) => t.id !== tagId);
      return {
        tagsByEmulator: { ...s.tagsByEmulator, [emulatorId]: list },
        emulators: s.emulators.map((e) => (e.id === emulatorId ? { ...e, tagsCount: list.length } : e)),
      };
    });
  },
  updateScript: async (id, content) => {
    const script = await uniEmuApi.scripts.patch(id, { content });
    set((s) => ({ scripts: upsertById(s.scripts, script) }));
  },
  createScript: async (input) => {
    const script = await uniEmuApi.scripts.create(input);
    set((s) => ({ scripts: upsertById(s.scripts, script) }));
    return script.id;
  },
  deleteScript: async (id) => {
    await uniEmuApi.scripts.delete(id);
    set((s) => ({ scripts: s.scripts.filter((sc) => sc.id !== id) }));
  },
  renameScript: async (id, name) => {
    const script = await uniEmuApi.scripts.patch(id, { name });
    set((s) => ({ scripts: upsertById(s.scripts, script) }));
  },
  uploadCncProgram: async (input) => {
    const program = input.scope === 'emulator' && input.emulatorId
      ? await uniEmuApi.cncPrograms.createForEmulator(input.emulatorId, input)
      : await uniEmuApi.cncPrograms.create(input);
    set((s) => ({ cncPrograms: upsertById(s.cncPrograms, program) }));
    return program.id;
  },
  updateCncProgram: async (id, patch) => {
    const program = await uniEmuApi.cncPrograms.patch(id, patch);
    set((s) => ({ cncPrograms: upsertById(s.cncPrograms, program) }));
  },
  deleteCncProgram: async (id) => {
    await uniEmuApi.cncPrograms.delete(id);
    set((s) => ({ cncPrograms: s.cncPrograms.filter((p) => p.id !== id) }));
  },
});

function upsertById<T extends { id: string }>(items: T[], item: T): T[] {
  return items.some((existing) => existing.id === item.id)
    ? items.map((existing) => (existing.id === item.id ? item : existing))
    : [...items, item];
}

function upsertEmulator(items: Emulator[], item: Emulator): Emulator[] {
  return sortEmulators(upsertById(items, item));
}

function sortEmulators(items: Emulator[]): Emulator[] {
  return [...items].sort(compareEmulators);
}

function compareEmulators(a: Emulator, b: Emulator): number {
  const byId = a.id.localeCompare(b.id, 'ru', { sensitivity: 'base' });
  return byId || a.status.localeCompare(b.status, 'ru', { sensitivity: 'base' });
}

function upsertEvent(items: SystemEvent[]): SystemEvent[] {
  const seen = new Set<string>();
  return items.filter((item) => {
    if (seen.has(item.id)) return false;
    seen.add(item.id);
    return true;
  });
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function applyTelemetryUpdate(
  set: Parameters<StateCreator<UniEmuState>>[0],
  get: Parameters<StateCreator<UniEmuState>>[1],
  update: RuntimeTelemetryUpdate,
) {
  set((s) => {
    const current = s.telemetryByEmulator[update.emulatorId] ?? [];
    const list = current.some((point) => point.timestamp === update.point.timestamp)
      ? current.map((point) => (point.timestamp === update.point.timestamp ? update.point : point))
      : [...current, update.point];

    return {
      telemetryByEmulator: {
        ...s.telemetryByEmulator,
        [update.emulatorId]: list.slice(-get().packetRetention),
      },
      online: true,
      apiError: null,
    };
  });
}

function applyTagValueUpdate(
  set: Parameters<StateCreator<UniEmuState>>[0],
  update: RuntimeTagValueUpdate,
) {
  set((s) => {
    const tags = s.tagsByEmulator[update.emulatorId];
    if (!tags) {
      return { online: true, apiError: null };
    }

    return {
      tagsByEmulator: {
        ...s.tagsByEmulator,
        [update.emulatorId]: tags.map((tag) => (
          tag.id === update.tagId
            ? { ...tag, preview: formatRuntimeTagValue(update.value) }
            : tag
        )),
      },
      online: true,
      apiError: null,
    };
  });
}

function formatRuntimeTagValue(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return JSON.stringify(value);
}

export const useUniEmuStore = PERSIST_STORE ? create<UniEmuState>()(
  persist(stateCreator, {
    name: 'uniemu-store',
    storage: createJSONStorage(() => localStorage),
    partialize: (s) => ({
      packetRetention: s.packetRetention,
    }),
  }),
) : create<UniEmuState>(stateCreator);
