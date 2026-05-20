import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('generator and scenario editors use separate calc type option lists', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const optionsSource = await readFile(join(componentDir, 'tag-scenario/calcTypeOptions.ts'), 'utf8');
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const scenarioEditorSource = await readFile(join(componentDir, 'tag-scenario/ScenarioEditor.tsx'), 'utf8');
  const calcFieldsSource = await readFile(join(componentDir, 'tag-scenario/CalcConfigFields.tsx'), 'utf8');

  assert.match(optionsSource, /export const GENERATOR_CALC_TYPES/);
  assert.match(optionsSource, /export const SCENARIO_CALC_TYPES/);

  const generatorList = optionsSource.match(/GENERATOR_CALC_TYPES[\s\S]*?satisfies readonly CalcType\[\];/)?.[0] ?? '';
  assert.doesNotMatch(generatorList, /'None'/);
  assert.doesNotMatch(generatorList, /'Static'/);
  assert.match(generatorList, /'Line'/);
  assert.match(generatorList, /'SquircleLate'/);

  const scenarioList = optionsSource.match(/SCENARIO_CALC_TYPES[\s\S]*?satisfies readonly CalcType\[\];/)?.[0] ?? '';
  assert.doesNotMatch(scenarioList, /'None'/);
  assert.doesNotMatch(scenarioList, /'Sequence'/);
  assert.match(scenarioList, /'Static'/);
  assert.match(scenarioList, /'Line'/);
  assert.match(scenarioList, /'SquircleLate'/);

  assert.match(drawerSource, /GENERATOR_CALC_TYPES/);
  assert.match(drawerSource, /GENERATOR_CALC_TYPES\.map\(\(c\) =>/);
  assert.doesNotMatch(drawerSource, /const CALC_TYPES: CalcType\[\]/);

  assert.match(scenarioEditorSource, /SCENARIO_CALC_TYPES/);
  assert.match(scenarioEditorSource, /calcTypes=\{SCENARIO_CALC_TYPES\}/);
  assert.match(calcFieldsSource, /calcTypes: readonly CalcType\[\]/);
  assert.match(calcFieldsSource, /calcTypes\.map\(\(c\) =>/);
  assert.doesNotMatch(calcFieldsSource, /const CALC_TYPES: CalcType\[\]/);
});
