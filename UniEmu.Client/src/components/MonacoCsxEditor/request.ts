import { API_BASE_URL } from './constants';
import type { CsxTextRange, ITextModel, Position } from './types';

type RequestOptions = {
  range?: CsxTextRange;
  newName?: string;
  includeDeclaration?: boolean;
  cancelKey?: string;
};

const activeRequests = new Map<string, AbortController>();

export async function request<T>(
  path: string,
  model: ITextModel,
  position?: Position,
  options: RequestOptions = {}
): Promise<T | null> {
  const cancelKey = options.cancelKey ?? `${path}:${model.uri.toString()}`;
  activeRequests.get(cancelKey)?.abort();

  const controller = new AbortController();
  activeRequests.set(cancelKey, controller);

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      signal: controller.signal,
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify({
        sourceCode: model.getValue(),
        documentUri: model.uri.toString(),
        position: position ? { line: position.lineNumber, column: position.column } : null,
        range: options.range,
        newName: options.newName,
        includeDeclaration: options.includeDeclaration ?? true,
      }),
    });

    if (!response.ok) return null;
    return (await response.json()) as T;
  } catch (error) {
    if (isAbortError(error)) return null;
    return null;
  } finally {
    if (activeRequests.get(cancelKey) === controller) {
      activeRequests.delete(cancelKey);
    }
  }
}

export function cancelRequestsForModel(model: ITextModel) {
  const suffix = `:${model.uri.toString()}`;
  for (const [key, controller] of activeRequests) {
    if (key.endsWith(suffix)) {
      controller.abort();
      activeRequests.delete(key);
    }
  }
}

function isAbortError(error: unknown) {
  return typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError';
}
