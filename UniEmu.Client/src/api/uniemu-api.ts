import type {
  CncProgram,
  CncScope,
  Emulator,
  EmulatorStatus,
  EmulatorTag,
  EventLevel,
  ScriptFile,
  ScriptScope,
  SystemEvent,
  TelemetryPoint,
} from '@/types/uniemu';
import { API_BASE_URL } from '@/lib/constants';

type CreateEmulatorRequest = Pick<Emulator, 'name' | 'targetUrl' | 'intervalSec' | 'protocolId'>;
type PatchEmulatorRequest = Partial<CreateEmulatorRequest>;
type CreateTagRequest = Omit<EmulatorTag, 'id'>;
type ReplaceTagRequest = Omit<EmulatorTag, 'id'>;
type CreateScriptRequest = { name: string; scope: ScriptScope; emulatorId?: string };
type PatchScriptRequest = { name?: string; content?: string };
type CreateCncProgramRequest = {
  name: string;
  scope: CncScope;
  emulatorId?: string;
  content: string;
  sizeBytes: number;
  isBinary?: boolean;
  description?: string;
};
type PatchCncProgramRequest = Partial<Pick<CncProgram, 'content' | 'description' | 'name'>>;
type PushEventRequest = {
  emulatorId: string;
  emulatorName: string;
  level: EventLevel;
  message: string;
  timestamp: string;
};
type DownloadFileResponse = { blob: Blob; fileName: string };

class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      Accept: 'application/json',
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new ApiError(extractApiErrorMessage(body, response.status, response.statusText), response.status, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

async function requestFile(path: string, fallbackFileName: string): Promise<DownloadFileResponse> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      Accept: 'application/xml',
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new ApiError(extractApiErrorMessage(body, response.status, response.statusText), response.status, body);
  }

  const contentDisposition = response.headers.get('content-disposition');
  return {
    blob: await response.blob(),
    fileName: getDownloadFileName(contentDisposition) ?? fallbackFileName,
  };
}

function getDownloadFileName(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (utf8Match?.[1]) return decodeURIComponent(utf8Match[1].trim());

  const plainMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
  return plainMatch?.[1]?.trim() || null;
}

function extractApiErrorMessage(body: string, status: number, statusText: string): string {
  const fallback = `API request failed: ${status} ${statusText}`;
  const trimmed = body.trim();
  if (!trimmed) return fallback;

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (typeof parsed === 'string' && parsed.trim()) return parsed;
    if (parsed && typeof parsed === 'object') {
      const message = 'message' in parsed ? parsed.message : undefined;
      if (typeof message === 'string' && message.trim()) return message;

      const title = 'title' in parsed ? parsed.title : undefined;
      if (typeof title === 'string' && title.trim()) return title;
    }
  } catch {
    return trimmed;
  }

  return trimmed || fallback;
}

const query = (params: Record<string, string | number | undefined>) => {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined) search.set(key, String(value));
  });
  const value = search.toString();
  return value ? `?${value}` : '';
};

export const uniEmuApi = {
  emulators: {
    list: () => request<Emulator[]>('/api/emulators'),
    get: (emulatorId: string) => request<Emulator>(`/api/emulators/${encodeURIComponent(emulatorId)}`),
    create: (body: CreateEmulatorRequest) =>
      request<Emulator>('/api/emulators', { method: 'POST', body: JSON.stringify(body) }),
    patch: (emulatorId: string, body: PatchEmulatorRequest) =>
      request<Emulator>(`/api/emulators/${encodeURIComponent(emulatorId)}`, {
        method: 'PATCH',
        body: JSON.stringify(body),
      }),
    setStatus: (emulatorId: string, status: EmulatorStatus) =>
      request<Emulator>(`/api/emulators/${encodeURIComponent(emulatorId)}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      }),
    downloadDispatcherTemplate: (emulatorId: string) =>
      requestFile(
        `/api/emulators/${encodeURIComponent(emulatorId)}/dispatcher-template`,
        `Universal_template_machineID_${emulatorId}.xml`,
      ),
    delete: (emulatorId: string) =>
      request<void>(`/api/emulators/${encodeURIComponent(emulatorId)}`, { method: 'DELETE' }),
  },
  tags: {
    list: (emulatorId: string) =>
      request<EmulatorTag[]>(`/api/emulators/${encodeURIComponent(emulatorId)}/tags`),
    create: (emulatorId: string, body: CreateTagRequest) =>
      request<EmulatorTag>(`/api/emulators/${encodeURIComponent(emulatorId)}/tags`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    replace: (emulatorId: string, tagId: string, body: ReplaceTagRequest) =>
      request<EmulatorTag>(
        `/api/emulators/${encodeURIComponent(emulatorId)}/tags/${encodeURIComponent(tagId)}`,
        { method: 'PATCH', body: JSON.stringify(body) },
      ),
    delete: (emulatorId: string, tagId: string) =>
      request<void>(`/api/emulators/${encodeURIComponent(emulatorId)}/tags/${encodeURIComponent(tagId)}`, {
        method: 'DELETE',
      }),
  },
  scripts: {
    list: () => request<ScriptFile[]>('/api/scripts'),
    create: (body: CreateScriptRequest) =>
      request<ScriptFile>('/api/scripts', { method: 'POST', body: JSON.stringify(body) }),
    patch: (scriptId: string, body: PatchScriptRequest) =>
      request<ScriptFile>(`/api/scripts/${encodeURIComponent(scriptId)}`, {
        method: 'PATCH',
        body: JSON.stringify(body),
      }),
    delete: (scriptId: string) => request<void>(`/api/scripts/${encodeURIComponent(scriptId)}`, { method: 'DELETE' }),
  },
  cncPrograms: {
    list: () => request<CncProgram[]>('/api/cnc-programs'),
    create: (body: CreateCncProgramRequest) =>
      request<CncProgram>('/api/cnc-programs', { method: 'POST', body: JSON.stringify(body) }),
    createForEmulator: (emulatorId: string, body: CreateCncProgramRequest) =>
      request<CncProgram>(`/api/emulators/${encodeURIComponent(emulatorId)}/cnc-programs`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    patch: (programId: string, body: PatchCncProgramRequest) =>
      request<CncProgram>(`/api/cnc-programs/${encodeURIComponent(programId)}`, {
        method: 'PATCH',
        body: JSON.stringify(body),
      }),
    delete: (programId: string) =>
      request<void>(`/api/cnc-programs/${encodeURIComponent(programId)}`, { method: 'DELETE' }),
  },
  telemetry: {
    list: (emulatorId: string, points = 60) =>
      request<TelemetryPoint[]>(`/api/emulators/${encodeURIComponent(emulatorId)}/telemetry${query({ points })}`),
  },
  events: {
    list: (limit = 200) => request<SystemEvent[]>(`/api/events${query({ limit })}`),
    create: (body: PushEventRequest) =>
      request<SystemEvent>('/api/events', { method: 'POST', body: JSON.stringify(body) }),
  },
};

export { ApiError };
