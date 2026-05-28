import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const storageDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'src', 'components', 'storage');
const srcDir = join(storageDir, '..', '..');

test('shared storage components expose the common explorer primitives', async () => {
  const [layout, row, group, emptyHint] = await Promise.all([
    readFile(join(storageDir, 'StorageExplorerLayout.tsx'), 'utf8'),
    readFile(join(storageDir, 'StorageFileRow.tsx'), 'utf8'),
    readFile(join(storageDir, 'StorageTreeGroup.tsx'), 'utf8'),
    readFile(join(storageDir, 'StorageEmptyHint.tsx'), 'utf8'),
  ]);

  assert.match(layout, /export function StorageExplorerLayout/);
  assert.match(layout, /grid-cols-\[300px_1fr\]/);
  assert.match(layout, /searchPlaceholder: string/);

  assert.match(row, /export interface StorageFileRowAction/);
  assert.match(row, /export function StorageFileRow/);
  assert.match(row, /meta\?: React\.ReactNode/);
  assert.match(row, /confirmDeleteMessage\?: string/);
  assert.match(row, /const \[editingName, setEditingName\] = useState\(false\)/);
  assert.match(row, /onRename\(draftName\.trim\(\)\)/);
  assert.match(row, /e\.key === 'Enter'/);
  assert.match(row, /e\.key === 'Escape'/);

  assert.match(group, /export function StorageTreeGroup/);
  assert.match(group, /onDrop\?: \(files: FileList\) => void/);
  assert.match(group, /dragActive\?: boolean/);

  assert.match(emptyHint, /export function StorageEmptyHint/);
});

test('scripts and cnc pages consume the shared storage primitives', async () => {
  const [scriptsPage, cncPage] = await Promise.all([
    readFile(join(srcDir, 'routes', 'scripts', 'components', 'ScriptsPage.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'cnc', 'components', 'CncStoragePage.tsx'), 'utf8'),
  ]);

  for (const source of [scriptsPage, cncPage]) {
    assert.match(source, /StorageExplorerLayout/);
    assert.match(source, /StorageTreeGroup/);
    assert.match(source, /StorageFileRow/);
    assert.match(source, /StorageEmptyHint/);
  }
});

test('storage sidebar keeps long file and group names inside the pane', async () => {
  const [layout, row, group] = await Promise.all([
    readFile(join(storageDir, 'StorageExplorerLayout.tsx'), 'utf8'),
    readFile(join(storageDir, 'StorageFileRow.tsx'), 'utf8'),
    readFile(join(storageDir, 'StorageTreeGroup.tsx'), 'utf8'),
  ]);

  assert.match(layout, /overflow-y-auto overflow-x-hidden/);

  assert.match(row, /className=\{`group flex min-w-0 items-center gap-1 overflow-hidden/);
  assert.match(row, /<button[\s\S]*className="flex min-w-0 flex-1 items-center gap-2 overflow-hidden text-left"/);
  assert.match(row, /<span title=\{name\} className="min-w-0 flex-1 truncate">/);

  assert.match(group, /className="group\/row flex min-w-0 items-center gap-1 px-1 py-1"/);
  assert.match(group, /className="flex min-w-0 flex-1 items-center gap-1\.5 overflow-hidden/);
  assert.match(group, /<span title=\{label\} className="min-w-0 flex-1 truncate text-foreground">/);
  assert.match(group, /className="ml-auto shrink-0 rounded bg-muted\/60/);
  assert.match(group, /<div className="ml-4 min-w-0 overflow-hidden">/);
});
