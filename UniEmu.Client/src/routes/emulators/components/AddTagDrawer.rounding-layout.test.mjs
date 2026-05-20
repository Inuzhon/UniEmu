import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('double rounding setting uses compact inline controls', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'tag-editor/TagBasicsSection.tsx'),
    'utf8'
  );

  assert.match(source, /items-center justify-between gap-3 rounded-md/);
  assert.match(source, /checked=\{roundEnabled\}/);
  assert.match(source, /onFieldChange\('roundEnabled', checked\)/);
  assert.match(source, /className="h-8 w-20 font-mono"/);
  assert.match(source, /<span className="text-xs text-muted-foreground">/);
  assert.doesNotMatch(source, /<div className="w-28 space-y-1\.5">/);
});
