import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const componentDir = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
  '..',
  '..',
  '..',
  '..',
  'src',
  'routes',
  'emulators',
  'components',
  'tag-scenario'
);

test('scenario segment rows keep long labels from pushing action buttons', async () => {
  const source = await readFile(join(componentDir, 'ScenarioEditor.tsx'), 'utf8');

  assert.match(
    source,
    /className="grid min-w-0 grid-cols-1 gap-3 lg:grid-cols-\[minmax\(260px,320px\)_1fr\]"/
  );
  assert.match(
    source,
    /className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-md border border-border bg-background\/40"/
  );
  assert.doesNotMatch(source, /<ScrollArea/);
  assert.doesNotMatch(source, /@\/components\/ui\/scroll-area/);
  assert.match(
    source,
    /<div className="max-h-\[420px\] min-w-0 flex-1 overflow-y-auto overflow-x-hidden">/
  );
  assert.match(source, /<ul className="min-w-0 overflow-hidden p-1\.5">/);
  assert.match(source, /<li key=\{seg\.id\} className="min-w-0 overflow-hidden">/);
  assert.match(
    source,
    /'group block w-full min-w-0 overflow-hidden rounded-md border border-transparent px-2 py-1\.5 text-left transition-colors'/
  );
  assert.match(
    source,
    /className="mb-1 flex min-w-0 items-center justify-between gap-2 overflow-hidden"/
  );
  assert.match(source, /className="flex min-w-0 flex-1 items-center gap-1\.5 overflow-hidden"/);
  assert.match(source, /className="shrink-0 rounded bg-primary\/15/);
  assert.match(
    source,
    /<span\s+title=\{seg\.label \|\| undefined\}\s+className="min-w-0 flex-1 truncate text-xs font-medium"\s+>/
  );
  assert.match(
    source,
    /className="flex shrink-0 items-center gap-0\.5 opacity-0 transition-opacity group-hover:opacity-100"/
  );
});
