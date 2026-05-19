import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('scenario tags are saved with an interval trigger for continuous timeline calculation', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.doesNotMatch(drawerSource, /isScenario\s*\?\s*\{\s*mode:\s*'once'/);
  assert.match(drawerSource, /isScenario\s*\?\s*\{\s*mode:\s*'interval',\s*intervalValue:\s*1,\s*intervalUnit:\s*'sec'\s*\}/);
  assert.doesNotMatch(localizationSource, /scenarioTimelineTriggerHint: '.*при старте/);
});

test('wave period uses the same minimum in scenario preview and editors as the backend', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );
  const scenarioMathSource = await readFile(
    join(componentDir, 'tag-scenario/scenarioMath.ts'),
    'utf8'
  );

  assert.match(scenarioMathSource, /Math\.max\(period,\s*1\)/);
  assert.match(drawerSource, /setCalcPeriod\(Math\.max\(1,\s*Number\(e\.target\.value\) \|\| 1\)\)/);
  assert.match(calcFieldsSource, /set\(\{ period: Math\.max\(1,\s*Number\(e\.target\.value\) \|\| 1\) \}\)/);
});

test('edit mode reuses one fallback scenario for form state and dirty snapshot', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');

  assert.match(drawerSource, /const nextScenario = tag\.scenario \?\? \{\s*segments: \[defaultSegment\(\)\],\s*continueOnFormulaEnd: 'Repeat'(?: as const)?,\s*\};/);
  assert.match(drawerSource, /setScenario\(nextScenario\);/);
  assert.match(drawerSource, /scenario: nextScenario,/);
});
