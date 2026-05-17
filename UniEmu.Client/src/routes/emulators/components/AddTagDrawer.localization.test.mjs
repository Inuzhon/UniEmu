import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag edit drawer displays localized formula and base-card labels', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );
  const calcLabelsSource = await readFile(join(componentDir, 'tag-scenario/calcLabels.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(drawerSource, /getCalcTypeLabel\(c\)/);
  assert.match(calcFieldsSource, /getCalcTypeLabel\(c\)/);
  assert.match(drawerSource, /getTagTypeLabel\(tagType\)/);
  assert.match(drawerSource, /id: 'formulaScript'/);
  assert.match(drawerSource, /source === 'formulaScript'/);
  assert.match(localizationSource, /parameterKeyLabel: 'Ключ параметра'/);
  assert.match(localizationSource, /specialParameterLabel: 'Специальный параметр'/);
  assert.match(localizationSource, /dataTypeLabel: 'Тип данных'/);
  assert.match(calcLabelsSource, /Line: 'Линейная'/);
  assert.match(calcLabelsSource, /SquircleLate: 'Плавное завершение'/);
  assert.doesNotMatch(localizationSource, /amplitudeLabel: 'Amplitude'/);
  assert.doesNotMatch(localizationSource, /finishLabel: 'Finish'/);
  assert.doesNotMatch(localizationSource, /text45: 'Duration/);
});
