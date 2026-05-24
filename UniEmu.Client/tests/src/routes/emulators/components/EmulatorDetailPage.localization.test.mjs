import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('emulator tag table uses localized labels and wider static preview input', async () => {
  const componentDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..', 'src', 'routes', 'emulators', 'components');
  const tagsSource = await readFile(join(componentDir, 'EmulatorTagsTab.tsx'), 'utf8');
  const calcLabelsSource = await readFile(join(componentDir, 'tag-scenario/calcLabels.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(tagsSource, /getTagTypeLabel\(tag\.type\)/);
  assert.match(tagsSource, /getTagSourceLabel\(tag\.source\)/);
  assert.match(tagsSource, /getTagIntervalUnitLabel\(/);
  assert.match(tagsSource, /getCalcTypeLabel\(tag\.calc\?\.type \?\? 'None'\)/);
  assert.match(tagsSource, /emulatorDetailPage\.keyColumnLabel/);
  assert.match(tagsSource, /emulatorDetailPage\.calcColumnLabel/);
  assert.match(tagsSource, /className="h-7 w-48 max-w-\[18rem\] px-2 py-1 font-mono text-xs"/);

  assert.match(calcLabelsSource, /export const TAG_SOURCE_LABELS/);
  assert.match(calcLabelsSource, /static: 'Статичное'/);
  assert.match(calcLabelsSource, /generator: 'Генератор'/);
  assert.match(calcLabelsSource, /scenario: 'Сценарий'/);
  assert.match(calcLabelsSource, /sec: 'сек'/);
  assert.match(calcLabelsSource, /min: 'мин'/);

  assert.match(localizationSource, /keyColumnLabel: 'Ключ'/);
  assert.match(localizationSource, /calcColumnLabel: 'Расчёт'/);
  assert.doesNotMatch(tagsSource, />Key</);
  assert.doesNotMatch(tagsSource, />Calc</);
});
