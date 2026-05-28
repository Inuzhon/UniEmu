import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('scenario tags are saved with an interval trigger for continuous timeline calculation', async () => {
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
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.doesNotMatch(utilsSource, /isScenario\s*\?\s*\{\s*mode:\s*'once'/);
  assert.match(
    utilsSource,
    /isScenario\s*\?\s*\{\s*mode:\s*'interval',\s*event:\s*null,\s*cron:\s*null,\s*intervalValue:\s*1,\s*intervalUnit:\s*'sec'\s*\}/
  );
  assert.match(utilsSource, /event:\s*null,\s*cron:\s*null,\s*intervalValue:\s*null,\s*intervalUnit:\s*null/);
  assert.match(utilsSource, /scenario:\s*isScenario \? buildScenarioPayload\(form\.scenario\) : null/);
  assert.match(utilsSource, /specialParameter:.*: null/);
  assert.match(utilsSource, /description:.*\|\| null/);
  assert.doesNotMatch(localizationSource, /scenarioTimelineTriggerHint: '.*при старте/);
});

test('wave period uses the same minimum in scenario preview and editors as the backend', async () => {
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
  const calcSectionSource = await readFile(
    join(componentDir, 'tag-editor/TagCalcSection.tsx'),
    'utf8'
  );
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );
  const scenarioMathSource = await readFile(
    join(componentDir, 'tag-scenario/scenarioMath.ts'),
    'utf8'
  );

  assert.match(scenarioMathSource, /Math\.max\(period,\s*1\)/);
  assert.match(calcSectionSource, /<CalcConfigFields/);
  assert.match(calcFieldsSource, /clampPeriodSeconds/);
  assert.match(
    calcFieldsSource,
    /set\(\{ period: clampPeriodSeconds\(Number\(e\.target\.value\) \|\| 1\) \}\)/
  );
});

test('edit mode reuses one fallback scenario for form state and dirty snapshot', async () => {
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
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.match(utilsSource, /const nextScenario = tag\.scenario \?\? createDefaultScenario\(\);/);
  assert.match(utilsSource, /scenario: nextScenario,/);
  assert.match(utilsSource, /scenario: form\.scenario,/);
});

test('new tag scenarios start without a default segment', async () => {
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
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.match(utilsSource, /segments:\s*\[\]/);
  assert.doesNotMatch(utilsSource, /segments:\s*\[defaultSegment\(\)\]/);
});

test('adding scenario segments assigns an automatic Russian label', async () => {
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
  const scenarioEditorSource = await readFile(
    join(componentDir, 'tag-scenario/ScenarioEditor.tsx'),
    'utf8'
  );

  assert.match(scenarioEditorSource, /const nextSegmentLabel = /);
  assert.match(scenarioEditorSource, /defaultSegmentLabel/);
  assert.match(scenarioEditorSource, /nextSegmentLabel\(segments\)/);
});

test('tag drawer duplicate validation ignores realtime preview-only tag updates', async () => {
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

  assert.match(drawerSource, /useShallow/);
  assert.match(drawerSource, /TAG_IDENTITY_SEPARATOR/);
  assert.match(drawerSource, /tagIdentityRows/);
  assert.doesNotMatch(
    drawerSource,
    /const tagsByEmulator = useUniEmuStore\(\(s\) => s\.tagsByEmulator\);/
  );
  assert.doesNotMatch(drawerSource, /const emulatorTags = tagsByEmulator\[emulatorId\] \?\? \[\];/);
});
