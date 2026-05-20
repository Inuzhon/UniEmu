import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag edit drawer displays localized formula and base-card labels', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const basicsSource = await readFile(join(componentDir, 'tag-editor/TagBasicsSection.tsx'), 'utf8');
  const constantsSource = await readFile(join(componentDir, 'tag-editor/constants.ts'), 'utf8');
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');
  const calcFieldsSource = await readFile(
    join(componentDir, 'tag-scenario/CalcConfigFields.tsx'),
    'utf8'
  );
  const calcLabelsSource = await readFile(join(componentDir, 'tag-scenario/calcLabels.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(calcFieldsSource, /getCalcTypeLabel\(c\)/);
  assert.match(basicsSource, /getTagTypeLabel\(tagType\)/);
  assert.match(constantsSource, /id: 'formulaScript'/);
  assert.match(utilsSource, /source === 'formulaScript'/);
  assert.match(drawerSource, /tagsByEmulator/);
  assert.match(drawerSource, /duplicateNameError/);
  assert.match(drawerSource, /duplicateKeyError/);
  assert.match(drawerSource, /normalizeTagIdentity/);
  assert.match(localizationSource, /duplicateNameError: 'Имя тега уже используется в этом эмуляторе'/);
  assert.match(localizationSource, /duplicateKeyError: 'Ключ тега уже используется в этом эмуляторе'/);
  assert.match(localizationSource, /tagKeyLabel: 'Ключ тега'/);
  assert.match(localizationSource, /specialParameterLabel: 'Специальный параметр'/);
  assert.match(localizationSource, /dataTypeLabel: 'Тип данных'/);
  assert.match(calcLabelsSource, /Line: 'Линейная'/);
  assert.match(calcLabelsSource, /SquircleLate: 'Плавное завершение'/);
  assert.doesNotMatch(localizationSource, /amplitudeLabel: 'Amplitude'/);
  assert.doesNotMatch(localizationSource, /finishLabel: 'Finish'/);
  assert.doesNotMatch(localizationSource, /text45: 'Duration/);
});
