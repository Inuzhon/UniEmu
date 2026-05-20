import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('scenario calc uses Static instead of Text and renders static value input by tag type', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const scenarioSectionSource = await readFile(join(componentDir, 'tag-editor/TagScenarioSection.tsx'), 'utf8');
  const scenarioEditorSource = await readFile(join(componentDir, 'tag-scenario/ScenarioEditor.tsx'), 'utf8');
  const calcFieldsSource = await readFile(join(componentDir, 'tag-scenario/CalcConfigFields.tsx'), 'utf8');
  const calcLabelsSource = await readFile(join(componentDir, 'tag-scenario/calcLabels.ts'), 'utf8');
  const typesSource = await readFile(join(componentDir, '../../../types/uniemu.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(typesSource, /\|\s*'Static'/);
  assert.doesNotMatch(typesSource, /\|\s*'Text'/);
  assert.match(calcLabelsSource, /Static: 'Статическое значение'/);
  assert.doesNotMatch(calcLabelsSource, /Text: 'Текст'/);

  assert.match(drawerSource, /<TagScenarioSection[\s\S]*scenario=\{form\.scenario\}[\s\S]*tagType=\{form\.type\}/);
  assert.match(scenarioSectionSource, /<ScenarioEditor value=\{scenario\} onChange=\{onChange\} tagType=\{tagType\} \/>/);
  assert.match(scenarioEditorSource, /tagType: TagType/);
  assert.match(scenarioEditorSource, /<CalcConfigFields[\s\S]*tagType=\{tagType\}/);

  assert.match(calcFieldsSource, /'Static'/);
  assert.doesNotMatch(calcFieldsSource, /'Text'/);
  assert.match(calcFieldsSource, /const isStatic = value\.type === 'Static'/);
  assert.match(calcFieldsSource, /calcConfigFields\.formulaTypeLabel/);
  assert.match(
    calcFieldsSource,
    /<Label className=\{labelCls\}>\s*\{localization\.routes\.emulators\.components\.tagScenario\.calcConfigFields\.formulaTypeLabel\}\s*<\/Label>/
  );
  assert.match(calcFieldsSource, /tagType === 'bool'/);
  assert.match(calcFieldsSource, /checked=\{value\.start === 'true'\}/);
  assert.match(calcFieldsSource, /set\(\{ start: checked \? 'true' : 'false' \}\)/);
  assert.match(calcFieldsSource, /sanitizeStaticValue\(tagType, e\.target\.value\)/);
  assert.match(calcFieldsSource, /calcConfigFields\.valueLabel/);
  assert.match(localizationSource, /formulaTypeLabel: 'Формула расчёта'/);
  assert.match(localizationSource, /valueLabel: 'Значение'/);
});
