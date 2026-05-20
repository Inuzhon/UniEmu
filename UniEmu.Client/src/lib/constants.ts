export const API_BASE_URL =
  (typeof import.meta !== 'undefined' &&
    (import.meta as { env?: Record<string, string | undefined> }).env?.VITE_API_BASE_URL) ||
  '';

export const TELEMETRY_PACKET_RETENTION_LIMIT = 3000;

export const TELEMETRY_CHART_VISIBLE_PACKET_COUNT = 60;

export const TELEMETRY_LINE_COLORS = [
  'oklch(0.78 0.16 195)',
  'oklch(0.82 0.16 80)',
  'oklch(0.78 0.18 155)',
  'oklch(0.75 0.18 25)',
  'oklch(0.72 0.14 285)',
  'oklch(0.8 0.12 120)',
  'oklch(0.7 0.2 335)',
  'oklch(0.76 0.15 230)',
  'oklch(0.84 0.14 105)',
  'oklch(0.74 0.19 45)',
  'oklch(0.68 0.16 260)',
  'oklch(0.8 0.13 15)',
  'oklch(0.73 0.17 175)',
  'oklch(0.79 0.15 310)',
  'oklch(0.71 0.14 135)',
  'oklch(0.83 0.13 65)',
] as const;
