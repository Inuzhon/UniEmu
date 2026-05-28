import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag drawer centralizes compatibility rules for special parameters, sources, and scenario formulas', async () => {
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
  const validationSource = await readFile(
    join(componentDir, 'tag-editor/tagValidation.ts'),
    'utf8'
  );
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const basicsSource = await readFile(
    join(componentDir, 'tag-editor/TagBasicsSection.tsx'),
    'utf8'
  );
  const scenarioEditorSource = await readFile(
    join(componentDir, 'tag-scenario/ScenarioEditor.tsx'),
    'utf8'
  );

  assert.match(validationSource, /TEXT_SPECIAL_PARAMETERS/);
  assert.match(validationSource, /'PrgName'/);
  assert.match(validationSource, /'FrameText'/);
  assert.match(validationSource, /'FrameNum'/);
  assert.match(validationSource, /getAllowedTagTypes/);
  assert.match(validationSource, /getAllowedTagSources/);
  assert.match(validationSource, /getScenarioCalcTypes/);
  assert.match(validationSource, /normalizeTagEditorForm/);
  assert.match(validationSource, /getTagValidationErrors/);
  assert.match(validationSource, /MAX_DISTORTION_PERCENT/);
  assert.match(validationSource, /clampDistortionPercent/);
  assert.match(validationSource, /Сценарий должен содержать хотя бы один участок/);

  assert.match(drawerSource, /normalizeTagEditorForm/);
  assert.match(drawerSource, /getTagValidationErrors/);
  assert.match(drawerSource, /validationErrors\.length === 0/);

  assert.match(basicsSource, /getAllowedTagTypes/);
  assert.match(basicsSource, /getAllowedTagSources/);
  assert.match(basicsSource, /TAG_EDITOR_TYPES\.filter/);
  assert.match(basicsSource, /TAG_EDITOR_SOURCES\.filter/);

  assert.match(scenarioEditorSource, /getScenarioCalcTypes/);
  assert.match(scenarioEditorSource, /calcTypes=\{scenarioCalcTypes\}/);
});

test('tag drawer clamps unsafe numeric generator inputs before submit', async () => {
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
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );
  const scenarioEditorSource = await readFile(
    join(componentDir, 'tag-scenario/ScenarioEditor.tsx'),
    'utf8'
  );
  const triggerSource = await readFile(
    join(componentDir, 'tag-editor/TagTriggerSection.tsx'),
    'utf8'
  );
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.match(calcFieldsSource, /clampDistortionPercent/);
  assert.match(calcFieldsSource, /max=\{100\}/);
  assert.match(calcFieldsSource, /clampDurationSeconds/);
  assert.match(calcFieldsSource, /clampCurvature/);
  assert.match(scenarioEditorSource, /clampDurationSeconds/);
  assert.match(triggerSource, /clampIntervalValue/);
  assert.match(utilsSource, /segments\.every/);
});

test('tag API errors expose backend validation messages to the form', async () => {
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
  const apiSource = await readFile(join(componentDir, '../../../api/uniemu-api.ts'), 'utf8');

  assert.match(apiSource, /extractApiErrorMessage/);
  assert.match(apiSource, /extractValidationErrors/);
  assert.match(apiSource, /humanizeValidationError/);
  assert.match(apiSource, /Триггер вычисления/);
  assert.match(apiSource, /throw new ApiError\(\s*extractApiErrorMessage\(body/);
  assert.match(drawerSource, /submitError/);
  assert.match(drawerSource, /formErrorMessages/);
  assert.match(drawerSource, /role="alert"/);
  assert.match(drawerSource, /sticky top-0/);
  assert.match(drawerSource, /catch \(error\)/);
  assert.match(drawerSource, /error instanceof Error\s*\?\s*error\.message/);
});
