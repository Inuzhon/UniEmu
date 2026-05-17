import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('emulator config editor keeps draft values during realtime runtime updates', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EditEmulatorDrawer.tsx'), 'utf8');

  assert.match(source, /const editorSessionKey = open && emulator \? emulator\.id : null;/);
  assert.match(source, /const emulatorRef = useRef<Emulator \| null>\(null\);/);
  assert.match(source, /emulatorRef\.current = emulator;/);
  assert.match(source, /useEffect\(\(\) => \{/);
  assert.match(source, /if \(!currentEmulator \|\| !editorSessionKey\) return;/);
  assert.match(source, /\}, \[editorSessionKey\]\);/);
  assert.doesNotMatch(source, /\}, \[emulator, open\]\);/);
});
