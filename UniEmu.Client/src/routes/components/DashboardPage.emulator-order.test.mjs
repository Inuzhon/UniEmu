import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('dashboard does not sort emulator cards by status first', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'DashboardPage.tsx'),
    'utf8'
  );

  assert.doesNotMatch(source, /const order: Record<string, number> = \{ Error: 0, Running: 1, Idle: 2, Stopped: 3 \};/);
  assert.doesNotMatch(source, /\(order\[aStatus\] \?\? 9\) - \(order\[bStatus\] \?\? 9\)/);
});
