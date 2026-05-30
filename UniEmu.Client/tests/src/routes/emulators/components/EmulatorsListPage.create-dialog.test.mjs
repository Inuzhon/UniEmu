import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('create emulator dialog resets draft values when closed without creating', async () => {
  const source = await readFile(join(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..', 'src', 'routes', 'emulators', 'components'), 'EmulatorsListPage.tsx'), 'utf8');

  assert.match(source, /const defaultEmulator = \(defaultTargetUrl: string\)/);
  assert.match(source, /targetUrl: defaultTargetUrl/);
  assert.match(source, /const appSettings = useUniEmuStore\(\(s\) => s\.appSettings\);/);
  assert.match(source, /const handleDialogOpenChange = \(open: boolean\) => \{/);
  assert.match(source, /if \(!open\) \{\s*setForm\(defaultEmulator\(appSettings\.defaultTargetUrl\)\);\s*\}/);
  assert.match(source, /<Dialog open=\{dialogOpen\} onOpenChange=\{handleDialogOpenChange\}>/);
});
