import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('static value editor follows selected data type', async () => {
  const componentDir = join(
    dirname(fileURLToPath(import.meta.url)),
    '..',
    '..',
    '..',
    '..',
    '..',
    'src',
    'routes',
    'emulators',
    'components'
  );
  const basicsSource = await readFile(
    join(componentDir, 'tag-editor/TagBasicsSection.tsx'),
    'utf8'
  );
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.match(basicsSource, /const renderStaticValueInput = \(\) =>/);
  assert.match(basicsSource, /type === 'bool'/);
  assert.match(basicsSource, /checked=\{staticValue === 'true'\}/);
  assert.match(basicsSource, /onFieldChange\('staticValue', checked \? 'true' : 'false'\)/);
  assert.match(
    basicsSource,
    /inputMode=\{type === 'int' \? 'numeric' : type === 'double' \? 'decimal' : undefined\}/
  );
  assert.match(basicsSource, /sanitizeStaticValue\(type, event\.target\.value\)/);
  assert.match(utilsSource, /const normalizedPreview =/);
  assert.match(utilsSource, /form\.source === 'static' && form\.type === 'bool'/);
  assert.match(utilsSource, /preview: normalizedPreview/);
});

test('empty static string value is saved as empty instead of display dash', async () => {
  const source = await readFile(
    join(
      join(
        dirname(fileURLToPath(import.meta.url)),
        '..',
        '..',
        '..',
        '..',
        '..',
        'src',
        'routes',
        'emulators',
        'components'
      ),
      'tag-editor/tagEditorUtils.ts'
    ),
    'utf8'
  );

  assert.doesNotMatch(source, /staticValue \|\|/);
  assert.match(source, /form\.source === 'static'\s*\?\s*form\.staticValue/);
});

test('program name special parameters use CNC program picker instead of plain input', async () => {
  const componentDir = join(
    dirname(fileURLToPath(import.meta.url)),
    '..',
    '..',
    '..',
    '..',
    '..',
    'src',
    'routes',
    'emulators',
    'components'
  );
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const basicsSource = await readFile(
    join(componentDir, 'tag-editor/TagBasicsSection.tsx'),
    'utf8'
  );

  assert.match(basicsSource, /const isProgramNameParameter =/);
  assert.match(
    basicsSource,
    /specialParameter === 'PrgName' \|\| specialParameter === 'Subprogram'/
  );
  assert.match(drawerSource, /const visibleCncPrograms = useMemo/);
  assert.match(drawerSource, /program\.scope === 'shared'/);
  assert.match(drawerSource, /program\.scope === 'emulator' && program\.emulatorId === emulatorId/);
  assert.match(basicsSource, /const renderProgramNamePicker = \(\) =>/);
  assert.match(basicsSource, /renderProgramNamePicker\(\)/);
  assert.match(
    basicsSource,
    /sharedCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/
  );
  assert.match(
    basicsSource,
    /emulatorCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/
  );
  assert.match(basicsSource, /programPickerSelectPlaceholder/);
  assert.match(basicsSource, /programPickerSharedGroup/);
  assert.match(basicsSource, /programPickerEmulatorGroup/);
  assert.match(basicsSource, /programPickerClearSelection/);
  assert.match(basicsSource, /onFieldChange\('staticValue', ''\)/);
  assert.match(basicsSource, /onWheel=\{\(event\) =>/);
  assert.match(basicsSource, /event\.currentTarget\.scrollTop \+= event\.deltaY/);
  assert.match(basicsSource, /renderStaticValueInput\(\)/);
});

test('scenario segment static values use CNC program picker for program name special parameters', async () => {
  const componentDir = join(
    dirname(fileURLToPath(import.meta.url)),
    '..',
    '..',
    '..',
    '..',
    '..',
    'src',
    'routes',
    'emulators',
    'components'
  );
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const scenarioSectionSource = await readFile(
    join(componentDir, 'tag-editor/TagScenarioSection.tsx'),
    'utf8'
  );
  const scenarioEditorSource = await readFile(
    join(componentDir, 'tag-scenario/ScenarioEditor.tsx'),
    'utf8'
  );
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );

  assert.match(
    drawerSource,
    /<TagScenarioSection[\s\S]*specialParameter=\{form\.specialParameter\}/
  );
  assert.match(drawerSource, /<TagScenarioSection[\s\S]*visibleCncPrograms=\{visibleCncPrograms\}/);
  assert.match(drawerSource, /<TagScenarioSection[\s\S]*sharedCncPrograms=\{sharedCncPrograms\}/);
  assert.match(
    drawerSource,
    /<TagScenarioSection[\s\S]*emulatorCncPrograms=\{emulatorCncPrograms\}/
  );

  assert.match(scenarioSectionSource, /specialParameter: SpecialParameter/);
  assert.match(scenarioSectionSource, /visibleCncPrograms: CncProgram\[\]/);
  assert.match(
    scenarioSectionSource,
    /<ScenarioEditor[\s\S]*specialParameter=\{specialParameter\}/
  );
  assert.match(
    scenarioSectionSource,
    /<ScenarioEditor[\s\S]*visibleCncPrograms=\{visibleCncPrograms\}/
  );

  assert.match(scenarioEditorSource, /specialParameter: SpecialParameter/);
  assert.match(scenarioEditorSource, /visibleCncPrograms: CncProgram\[\]/);
  assert.match(
    scenarioEditorSource,
    /<CalcConfigFields[\s\S]*specialParameter=\{specialParameter\}/
  );
  assert.match(
    scenarioEditorSource,
    /<CalcConfigFields[\s\S]*visibleCncPrograms=\{visibleCncPrograms\}/
  );

  assert.match(calcFieldsSource, /const isProgramNameParameter =/);
  assert.match(
    calcFieldsSource,
    /specialParameter === 'PrgName' \|\| specialParameter === 'Subprogram'/
  );
  assert.match(calcFieldsSource, /const renderProgramNamePicker = \(\) =>/);
  assert.match(calcFieldsSource, /set\(\{ start: program\.name \}\)/);
  assert.match(calcFieldsSource, /set\(\{ start: '' \}\)/);
  assert.match(calcFieldsSource, /renderProgramNamePicker\(\)/);
  assert.match(
    calcFieldsSource,
    /sharedCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/
  );
  assert.match(
    calcFieldsSource,
    /emulatorCncPrograms\.map\(\(program\) => renderProgramOption\(program\)\)/
  );
});
