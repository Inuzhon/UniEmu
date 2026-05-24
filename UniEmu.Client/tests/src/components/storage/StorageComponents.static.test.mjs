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
