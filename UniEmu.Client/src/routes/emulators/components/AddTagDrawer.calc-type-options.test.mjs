import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('generator and scenario editors use separate calc type option lists', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const optionsSource = await readFile(join(componentDir, 'tag-scenario/calcTypeOptions.ts'), 'utf8');
  const calcSectionSource = await readFile(join(componentDir, 'tag-editor/TagCalcSection.tsx'), 'utf8');
  const scenarioEditorSource = await readFile(join(componentDir, 'tag-scenario/ScenarioEditor.tsx'), 'utf8');
  const calcFieldsSource = await readFile(join(componentDir, 'tag-scenario/CalcConfigFields.tsx'), 'utf8');
  const validationSource = await readFile(join(componentDir, 'tag-editor/tagValidation.ts'), 'utf8');

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

  assert.match(calcSectionSource, /GENERATOR_CALC_TYPES/);
  assert.match(calcSectionSource, /calcTypes=\{GENERATOR_CALC_TYPES\}/);
  assert.doesNotMatch(calcSectionSource, /const CALC_TYPES: CalcType\[\]/);

  assert.match(validationSource, /SCENARIO_CALC_TYPES/);
  assert.match(scenarioEditorSource, /getScenarioCalcTypes/);
  assert.match(scenarioEditorSource, /calcTypes=\{scenarioCalcTypes\}/);
  assert.match(calcFieldsSource, /calcTypes: readonly CalcType\[\]/);
  assert.match(calcFieldsSource, /calcTypes\.map\(\(c\) =>/);
  assert.doesNotMatch(calcFieldsSource, /const CALC_TYPES: CalcType\[\]/);
});
