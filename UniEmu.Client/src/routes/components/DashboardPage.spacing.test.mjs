import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('dashboard page keeps vertical rhythm compact enough for first viewport', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'DashboardPage.tsx'),
    'utf8'
  );

  assert.match(source, /className="space-y-6 p-4 md:p-5"/);
  assert.match(source, /className="relative mt-6 grid grid-cols-1 gap-4/);
  assert.match(source, /className="grid grid-cols-2 gap-2 lg:grid-cols-4"/);
});
