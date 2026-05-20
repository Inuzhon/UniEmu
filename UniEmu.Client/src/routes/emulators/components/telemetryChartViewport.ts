export type TelemetryYRange = {
  min: number;
  max: number;
};

export type TelemetryYViewport = {
  zoom: number;
  offset: number;
};

const MIN_Y_ZOOM = 1;
const MAX_Y_ZOOM = 64;
const WHEEL_ZOOM_STEP = 1.25;
const Y_RANGE_PADDING_RATIO = 0.1;

type TelemetryYPoint = {
  values?: Record<string, unknown>;
};

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function getTelemetryYBaseRange(points: TelemetryYPoint[]): TelemetryYRange | null {
  const values = points.flatMap((point) =>
    Object.values(point.values ?? {}).filter((value): value is number =>
      typeof value === 'number' && Number.isFinite(value)
    )
  );

  if (values.length === 0) return null;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min;
  const padding = span > 0 ? span * Y_RANGE_PADDING_RATIO : Math.max(Math.abs(max) * Y_RANGE_PADDING_RATIO, 1);

  return {
    min: min - padding,
    max: max + padding,
  };
}

export function getTelemetryYDomain(
  baseRange: TelemetryYRange | null,
  viewport: TelemetryYViewport
): [number, number] | undefined {
  if (!baseRange) return undefined;

  const baseSpan = baseRange.max - baseRange.min;
  if (baseSpan <= 0) return [baseRange.min, baseRange.max];

  const zoom = clamp(viewport.zoom, MIN_Y_ZOOM, MAX_Y_ZOOM);
  const visibleSpan = baseSpan / zoom;
  const center = (baseRange.min + baseRange.max) / 2 + viewport.offset;

  return [center - visibleSpan / 2, center + visibleSpan / 2];
}

export function zoomTelemetryYViewport(
  viewport: TelemetryYViewport,
  wheelDeltaY: number
): TelemetryYViewport {
  if (wheelDeltaY === 0) return viewport;

  const zoomFactor = wheelDeltaY < 0 ? WHEEL_ZOOM_STEP : 1 / WHEEL_ZOOM_STEP;
  return {
    ...viewport,
    zoom: clamp(Number((viewport.zoom * zoomFactor).toFixed(4)), MIN_Y_ZOOM, MAX_Y_ZOOM),
  };
}

export function panTelemetryYViewport(
  viewport: TelemetryYViewport,
  baseRange: TelemetryYRange,
  valueDelta: number
): TelemetryYViewport {
  const baseSpan = baseRange.max - baseRange.min;
  if (baseSpan <= 0 || viewport.zoom <= MIN_Y_ZOOM) return { zoom: viewport.zoom, offset: 0 };

  const visibleSpan = baseSpan / clamp(viewport.zoom, MIN_Y_ZOOM, MAX_Y_ZOOM);
  const maxOffset = Math.max((baseSpan - visibleSpan) / 2, 0);

  return {
    ...viewport,
    offset: clamp(viewport.offset + valueDelta, -maxOffset, maxOffset),
  };
}
