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

test('tag table uses CNC program picker for program name special parameters', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'EmulatorDetailPage.tsx'),
    'utf8'
  );

  assert.match(source, /const cncPrograms = useUniEmuStore\(\(s\) => s\.cncPrograms\)/);
  assert.match(source, /const visibleCncPrograms = useMemo/);
  assert.match(source, /program\.scope === 'shared'/);
  assert.match(source, /program\.scope === 'emulator' && program\.emulatorId === id/);
  assert.match(source, /const isProgramNameTag = \(t: EmulatorTag\) =>/);
  assert.match(source, /t\.specialParameter === 'PrgName' \|\| t\.specialParameter === 'Subprogram'/);
  assert.match(source, /const renderProgramPreviewPicker = \(t: EmulatorTag\) =>/);
  assert.match(source, /renderProgramPreviewPicker\(t\)/);
  assert.match(source, /preview: program\.name/);
  assert.match(source, /onWheel=\{\(event\) =>/);
  assert.match(source, /event\.currentTarget\.scrollTop \+= event\.deltaY/);
});
