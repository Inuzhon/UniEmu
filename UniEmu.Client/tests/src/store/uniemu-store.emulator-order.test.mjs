import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const sourcePath = join(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src', 'store'), 'uniemu-store.ts');

test('store keeps emulators ordered by id before status across loads and updates', async () => {
  const source = await readFile(sourcePath, 'utf8');

  assert.match(source, /function compareEmulators\(a: Emulator, b: Emulator\): number \{/);
  assert.match(source, /const byId = a\.id\.localeCompare\(b\.id,/);
  assert.match(source, /return byId \|\| a\.status\.localeCompare\(b\.status,/);
  assert.match(source, /emulators: sortEmulators\(emulators\),/);
  assert.match(source, /emulators: upsertEmulator\(s\.emulators, emulator\)/);
});
