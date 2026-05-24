import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('dashboard treats emulators with runtime errors as errored in summary counts', async () => {
  const source = await readFile(
    join(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'src', 'routes', 'components'), 'DashboardPage.tsx'),
    'utf8'
  );

  assert.match(source, /const hasRuntimeError = \(e: Emulator\) => e\.status === 'Error' \|\| !!e\.lastError;/);
  assert.match(source, /const running = emulators\.filter\(\(e\) => e\.status === 'Running' && !hasRuntimeError\(e\)\)\.length;/);
  assert.match(source, /const errors = emulators\.filter\(hasRuntimeError\)\.length;/);
  assert.doesNotMatch(source, /const order: Record<string, number> = \{ Error: 0, Running: 1, Idle: 2, Stopped: 3 \};/);
  assert.match(source, /status=\{hasRuntimeError\(e\) \? 'Error' : e\.status\}/);
});
