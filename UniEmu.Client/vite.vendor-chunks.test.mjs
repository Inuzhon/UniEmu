import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

test('chart vendor chunk includes d3 scale runtime dependencies', async () => {
  const source = await readFile(new URL('./vite.config.ts', import.meta.url), 'utf8');
  const chartGroupMatch = source.match(/name:\s*"vendor-charts"[\s\S]*?priority:\s*40/);

  assert.ok(chartGroupMatch, 'vendor-charts chunk group should be configured');
  assert.match(chartGroupMatch[0], /"d3-"/);
  assert.match(chartGroupMatch[0], /"internmap"/);
});
