import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('scenario chart tooltip uses theme-aware popover colors', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'ScenarioPreviewChart.tsx'),
    'utf8'
  );

  assert.match(source, /background: 'var\(--popover\)'/);
  assert.match(source, /border: '1px solid var\(--border\)'/);
  assert.match(source, /color: 'var\(--popover-foreground\)'/);
  assert.match(source, /labelStyle=\{\{ color: 'var\(--popover-foreground\)' \}\}/);
  assert.match(source, /itemStyle=\{\{ color: 'var\(--popover-foreground\)' \}\}/);
  assert.doesNotMatch(source, /background: 'oklch\(0\.22 0\.018 240\)'/);
});
