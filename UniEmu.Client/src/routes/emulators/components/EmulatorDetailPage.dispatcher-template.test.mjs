import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('emulator detail can download dispatcher XML template through store action', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const detailSource = await readFile(join(componentDir, 'EmulatorDetailPage.tsx'), 'utf8');
  const storeSource = await readFile(join(componentDir, '../../../store/uniemu-store.ts'), 'utf8');
  const apiSource = await readFile(join(componentDir, '../../../api/uniemu-api.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(apiSource, /downloadDispatcherTemplate: \(emulatorId: string\) =>/);
  assert.match(apiSource, /\/api\/emulators\/\$\{encodeURIComponent\(emulatorId\)\}\/dispatcher-template/);
  assert.match(apiSource, /response\.blob\(\)/);
  assert.match(apiSource, /content-disposition/i);

  assert.match(storeSource, /downloadDispatcherTemplate: \(emulatorId: string\) => Promise<void>/);
  assert.match(storeSource, /uniEmuApi\.emulators\.downloadDispatcherTemplate\(emulatorId\)/);
  assert.match(storeSource, /URL\.createObjectURL\(blob\)/);
  assert.match(storeSource, /a\.download = fileName/);
  assert.match(storeSource, /URL\.revokeObjectURL\(url\)/);

  assert.match(detailSource, /Download,/);
  assert.match(detailSource, /const downloadDispatcherTemplate = useUniEmuStore/);
  assert.match(detailSource, /handleDownloadDispatcherTemplate/);
  assert.match(detailSource, /emulatorDetailPage\.downloadDispatcherTemplate/);
  assert.match(detailSource, /<Download className="h-3\.5 w-3\.5" \/>/);

  assert.match(localizationSource, /downloadDispatcherTemplate:/);
});
