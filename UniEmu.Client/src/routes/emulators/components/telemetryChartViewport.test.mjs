import assert from 'node:assert/strict';
import { test } from 'node:test';

import {
  getTelemetryYBaseRange,
  getTelemetryYDomain,
  panTelemetryYViewport,
  zoomTelemetryYViewport,
} from './telemetryChartViewport.ts';

test('telemetry y viewport builds a padded domain from visible chart values', () => {
  const baseRange = getTelemetryYBaseRange([
    { values: { temperature: 10, pressure: 30 } },
    { values: { temperature: 20, pressure: 40 } },
  ]);

  assert.deepEqual(baseRange, { min: 7, max: 43 });
  assert.deepEqual(getTelemetryYDomain(baseRange, { zoom: 1, offset: 0 }), [7, 43]);
});

test('telemetry y viewport zooms around the current range center', () => {
  const baseRange = { min: 0, max: 100 };
  const viewport = zoomTelemetryYViewport({ zoom: 1, offset: 0 }, -100);

  assert.equal(viewport.zoom, 1.25);
  assert.deepEqual(getTelemetryYDomain(baseRange, viewport), [10, 90]);
});

test('telemetry y viewport pans vertically in value units and clamps to the base range', () => {
  const baseRange = { min: 0, max: 100 };
  const zoomed = { zoom: 2, offset: 0 };

  assert.deepEqual(getTelemetryYDomain(baseRange, panTelemetryYViewport(zoomed, baseRange, -10)), [15, 65]);
  assert.deepEqual(getTelemetryYDomain(baseRange, panTelemetryYViewport(zoomed, baseRange, 1000)), [50, 100]);
});
