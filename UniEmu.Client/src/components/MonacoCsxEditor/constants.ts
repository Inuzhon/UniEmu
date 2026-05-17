export const API_BASE_URL =
  (typeof import.meta !== 'undefined' &&
    (import.meta as { env?: Record<string, string | undefined> }).env?.VITE_API_BASE_URL) ||
  '';

export const MONACO_LANGUAGE_ID = 'csharp';
export const MARKER_OWNER = 'uniemu-csx';
