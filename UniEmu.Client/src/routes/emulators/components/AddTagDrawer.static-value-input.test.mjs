import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('static value editor follows selected data type', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const basicsSource = await readFile(join(componentDir, 'tag-editor/TagBasicsSection.tsx'), 'utf8');
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.match(basicsSource, /const renderStaticValueInput = \(\) =>/);
  assert.match(basicsSource, /type === 'bool'/);
  assert.match(basicsSource, /checked=\{staticValue === 'true'\}/);
  assert.match(basicsSource, /onFieldChange\('staticValue', checked \? 'true' : 'false'\)/);
  assert.match(basicsSource, /inputMode=\{type === 'int' \? 'numeric' : type === 'double' \? 'decimal' : undefined\}/);
  assert.match(basicsSource, /sanitizeStaticValue\(type, event\.target\.value\)/);
  assert.match(utilsSource, /const normalizedPreview =/);
  assert.match(utilsSource, /form\.source === 'static' && form\.type === 'bool'/);
  assert.match(utilsSource, /preview: normalizedPreview/);
});

test('empty static string value is saved as empty instead of display dash', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'tag-editor/tagEditorUtils.ts'),
    'utf8',
  );

  assert.doesNotMatch(source, /staticValue \|\|/);
  assert.match(source, /form\.source === 'static'\s*\?\s*form\.staticValue/);
});

test('program name special parameters use CNC program picker instead of plain input', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const basicsSource = await readFile(join(componentDir, 'tag-editor/TagBasicsSection.tsx'), 'utf8');

  assert.match(basicsSource, /const isProgramNameParameter =/);
  assert.match(basicsSource, /specialParameter === 'PrgName' \|\| specialParameter === 'Subprogram'/);
  assert.match(drawerSource, /const visibleCncPrograms = useMemo/);
  assert.match(drawerSource, /program\.scope === 'shared'/);
  assert.match(drawerSource, /program\.scope === 'emulator' && program\.emulatorId === emulatorId/);
  assert.match(basicsSource, /const renderProgramNamePicker = \(\) =>/);
  assert.match(basicsSource, /renderProgramNamePicker\(\)/);
  assert.match(basicsSource, /sharedCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/);
  assert.match(basicsSource, /emulatorCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/);
  assert.match(basicsSource, /programPickerSelectPlaceholder/);
  assert.match(basicsSource, /programPickerSharedGroup/);
  assert.match(basicsSource, /programPickerEmulatorGroup/);
  assert.match(basicsSource, /onWheel=\{\(event\) =>/);
  assert.match(basicsSource, /event\.currentTarget\.scrollTop \+= event\.deltaY/);
  assert.match(basicsSource, /renderStaticValueInput\(\)/);
});
