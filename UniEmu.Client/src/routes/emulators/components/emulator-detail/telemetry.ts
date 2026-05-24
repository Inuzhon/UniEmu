import type { EmulatorTag, TelemetryPoint } from '@/types/uniemu';

export type TelemetryChartPoint = {
  timestamp: string;
  time: string;
  values: Record<string, number>;
} & Record<string, string | number | Record<string, number>>;

export type PacketHistoryRow = {
  idx: number;
  timestamp: string;
  values: Record<string, string>;
};

export const TELEMETRY_HIDDEN_TAGS_STORAGE_PREFIX = 'uniemu.telemetry.hidden-tags.';

export const emptyTelemetry: TelemetryPoint[] = [];

export function getTelemetryHiddenTagsStorageKey(emulatorId: string): string {
  return `${TELEMETRY_HIDDEN_TAGS_STORAGE_PREFIX}${emulatorId}`;
}

function getTelemetryStorage(): Storage | null {
  if (typeof window === 'undefined') return null;

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

export function readHiddenTelemetryTagNames(emulatorId: string): Set<string> {
  const storage = getTelemetryStorage();
  if (!storage) return new Set();

  try {
    const raw = storage.getItem(getTelemetryHiddenTagsStorageKey(emulatorId));
    if (!raw) return new Set();

    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return new Set();

    return new Set(parsed.filter((name): name is string => typeof name === 'string'));
  } catch {
    return new Set();
  }
}

export function writeHiddenTelemetryTagNames(
  emulatorId: string,
  hiddenTelemetryTagNames: Set<string>,
): void {
  const storage = getTelemetryStorage();
  if (!storage) return;

  const key = getTelemetryHiddenTagsStorageKey(emulatorId);
  const values = [...hiddenTelemetryTagNames];

  try {
    if (values.length === 0) {
      storage.removeItem(key);
      return;
    }

    storage.setItem(key, JSON.stringify(values));
  } catch {
    // localStorage can be unavailable or full; chart visibility still works in memory.
  }
}

export function parseTelemetryNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const normalized = value.trim().replace(',', '.');
    if (normalized.length === 0) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

export function formatTelemetryValue(value: unknown): string {
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || !Number.isFinite(value)) return String(value);
  return Number(value.toFixed(2)).toLocaleString('ru-RU');
}

export function buildTelemetryChartPoints(
  telemetryPoints: TelemetryPoint[],
  visibleNumericTelemetryTags: EmulatorTag[],
): TelemetryChartPoint[] {
  return telemetryPoints.map((p) => {
    const values = Object.fromEntries(
      visibleNumericTelemetryTags.flatMap((t) => {
        const value = parseTelemetryNumber(p.values?.[t.name]);
        return value === null ? [] : [[t.name, value]];
      })
    );

    return {
      timestamp: p.timestamp,
      time: new Date(p.timestamp).toLocaleTimeString('ru-RU', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      }),
      values,
      ...values,
    };
  });
}

export function getTelemetryKeys(telemetry: TelemetryChartPoint[]): string[] {
  const keys = new Set<string>();
  telemetry.forEach((p) => {
    Object.entries(p.values ?? {}).forEach(([key, value]) => {
      if (Number.isFinite(value)) keys.add(key);
    });
  });
  return [...keys];
}

export function buildPacketHistory(
  telemetryPoints: TelemetryPoint[],
  enabledTagsForDispatcher: EmulatorTag[],
  packetRetention: number,
): PacketHistoryRow[] {
  const arr = telemetryPoints.map((p, i) => ({
    idx: i,
    timestamp: p.timestamp,
    values: enabledTagsForDispatcher.reduce<Record<string, string>>((acc, t) => {
      acc[t.name] = formatTelemetryValue(p.values?.[t.name]);
      return acc;
    }, {}),
  }));
  return arr.slice().reverse().slice(0, packetRetention);
}

export function areMonitoringTagsEqual(prev: EmulatorTag[], next: EmulatorTag[]): boolean {
  if (prev.length !== next.length) return false;

  return prev.every((tag, index) => {
    const nextTag = next[index];
    return tag.id === nextTag.id &&
      tag.name === nextTag.name &&
      tag.key === nextTag.key &&
      tag.type === nextTag.type &&
      tag.source === nextTag.source &&
      tag.enabled === nextTag.enabled &&
      tag.scenario === nextTag.scenario;
  });
}
