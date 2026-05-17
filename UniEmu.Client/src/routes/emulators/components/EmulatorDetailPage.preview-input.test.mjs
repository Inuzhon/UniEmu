import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

test('static numeric tag preview input does not use native number controls', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'EmulatorDetailPage.tsx'),
    'utf8'
  );

  assert.doesNotMatch(source, /type=\{t\.type === 'int' \|\| t\.type === 'double' \? 'number'/);
  assert.match(
    source,
    /inputMode=\{\s*t\.type === 'int' \? 'numeric' : t\.type === 'double' \? 'decimal' : undefined\s*\}/
  );
});
