import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('static value editor follows selected data type', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'AddTagDrawer.tsx'), 'utf8');

  assert.match(source, /const renderStaticValueInput = \(\) =>/);
  assert.match(source, /type === 'bool'/);
  assert.match(source, /checked=\{staticValue === 'true'\}/);
  assert.match(source, /setStaticValue\(checked \? 'true' : 'false'\)/);
  assert.match(source, /inputMode=\{type === 'int' \? 'numeric' : type === 'double' \? 'decimal' : undefined\}/);
  assert.match(source, /sanitizeStaticValue\(type, e\.target\.value\)/);
  assert.match(source, /const normalizedPreview =/);
  assert.match(source, /source === 'static' && type === 'bool'/);
  assert.match(source, /preview: normalizedPreview/);
});

test('empty static string value is saved as empty instead of display dash', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'AddTagDrawer.tsx'), 'utf8');

  assert.doesNotMatch(source, /staticValue \|\| ['"`]—['"`]/);
  assert.match(source, /source === 'static'\s*\?\s*staticValue/);
});

test('program name special parameters use CNC program picker instead of plain input', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'AddTagDrawer.tsx'), 'utf8');

  assert.match(source, /const isProgramNameParameter =/);
  assert.match(source, /specialParameter === 'PrgName' \|\| specialParameter === 'Subprogram'/);
  assert.match(source, /const visibleCncPrograms = useMemo/);
  assert.match(source, /program\.scope === 'shared'/);
  assert.match(source, /program\.scope === 'emulator' && program\.emulatorId === emulatorId/);
  assert.match(source, /const renderProgramNamePicker = \(\) =>/);
  assert.match(source, /renderProgramNamePicker\(\)/);
  assert.match(source, /sharedCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/);
  assert.match(source, /emulatorCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/);
  assert.match(source, /programPickerSelectPlaceholder/);
  assert.match(source, /programPickerSharedGroup/);
  assert.match(source, /programPickerEmulatorGroup/);
  assert.match(source, /onWheel=\{\(event\) =>/);
  assert.match(source, /event\.currentTarget\.scrollTop \+= event\.deltaY/);
  assert.match(source, /renderStaticValueInput\(\)/);
});
