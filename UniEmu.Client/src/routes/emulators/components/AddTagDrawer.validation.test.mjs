import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag drawer centralizes compatibility rules for special parameters, sources, and scenario formulas', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const validationSource = await readFile(join(componentDir, 'tag-editor/tagValidation.ts'), 'utf8');
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const basicsSource = await readFile(join(componentDir, 'tag-editor/TagBasicsSection.tsx'), 'utf8');
  const scenarioEditorSource = await readFile(join(componentDir, 'tag-scenario/ScenarioEditor.tsx'), 'utf8');

  assert.match(validationSource, /TEXT_SPECIAL_PARAMETERS/);
  assert.match(validationSource, /'PrgName'/);
  assert.match(validationSource, /'FrameText'/);
  assert.match(validationSource, /'FrameNum'/);
  assert.match(validationSource, /getAllowedTagTypes/);
  assert.match(validationSource, /getAllowedTagSources/);
  assert.match(validationSource, /getScenarioCalcTypes/);
  assert.match(validationSource, /normalizeTagEditorForm/);
  assert.match(validationSource, /getTagValidationErrors/);

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

test('tag API errors expose backend validation messages to the form', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const apiSource = await readFile(join(componentDir, '../../../api/uniemu-api.ts'), 'utf8');

  assert.match(apiSource, /extractApiErrorMessage/);
  assert.match(apiSource, /throw new ApiError\(extractApiErrorMessage\(body/);
  assert.match(drawerSource, /submitError/);
  assert.match(drawerSource, /catch \(error\)/);
  assert.match(drawerSource, /error instanceof Error\s*\?\s*error\.message/);
});
