import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

test('static numeric tag preview input does not use native number controls', async () => {
  const source = await readFile(
    join(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..', 'src', 'routes', 'emulators', 'components'), 'EmulatorTagsTab.tsx'),
    'utf8'
  );

  assert.doesNotMatch(source, /type=\{tag\.type === 'int' \|\| tag\.type === 'double' \? 'number'/);
  assert.match(
    source,
    /inputMode=\{\s*tag\.type === 'int' \? 'numeric' : tag\.type === 'double' \? 'decimal' : undefined\s*\}/
  );
});

test('tag table uses CNC program picker for program name special parameters', async () => {
  const componentDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..', 'src', 'routes', 'emulators', 'components');
  const source = [
    await readFile(join(componentDir, 'EmulatorDetailPage.tsx'), 'utf8'),
    await readFile(join(componentDir, 'EmulatorTagsTab.tsx'), 'utf8'),
  ].join('\n');

  assert.match(source, /const cncPrograms = useUniEmuStore\(\(s\) => s\.cncPrograms\)/);
  assert.match(source, /const visibleCncPrograms = useMemo/);
  assert.match(source, /program\.scope === 'shared'/);
  assert.match(source, /program\.scope === 'emulator' && program\.emulatorId === id/);
  assert.match(source, /function isProgramNameTag\(tag: EmulatorTag\): boolean/);
  assert.match(source, /tag\.specialParameter === 'PrgName' \|\| tag\.specialParameter === 'Subprogram'/);
  assert.match(source, /const renderProgramPreviewPicker = \(tag: EmulatorTag\) =>/);
  assert.match(source, /renderProgramPreviewPicker\(tag\)/);
  assert.match(source, /preview: program\.name/);
  assert.match(source, /onWheel=\{\(event\) =>/);
  assert.match(source, /event\.currentTarget\.scrollTop \+= event\.deltaY/);
});
