import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('emulator tag table uses localized labels and wider static preview input', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const detailSource = await readFile(join(componentDir, 'EmulatorDetailPage.tsx'), 'utf8');
  const calcLabelsSource = await readFile(join(componentDir, 'tag-scenario/calcLabels.ts'), 'utf8');
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(detailSource, /getTagTypeLabel\(t\.type\)/);
  assert.match(detailSource, /getTagSourceLabel\(t\.source\)/);
  assert.match(detailSource, /getTagIntervalUnitLabel\(/);
  assert.match(detailSource, /getCalcTypeLabel\(t\.calc\?\.type \?\? 'None'\)/);
  assert.match(detailSource, /emulatorDetailPage\.keyColumnLabel/);
  assert.match(detailSource, /emulatorDetailPage\.calcColumnLabel/);
  assert.match(detailSource, /className="h-7 w-48 max-w-\[18rem\] px-2 py-1 font-mono text-xs"/);

  assert.match(calcLabelsSource, /export const TAG_SOURCE_LABELS/);
  assert.match(calcLabelsSource, /static: 'Статичное'/);
  assert.match(calcLabelsSource, /generator: 'Генератор'/);
  assert.match(calcLabelsSource, /scenario: 'Сценарий'/);
  assert.match(calcLabelsSource, /sec: 'сек'/);
  assert.match(calcLabelsSource, /min: 'мин'/);

  assert.match(localizationSource, /keyColumnLabel: 'Ключ'/);
  assert.match(localizationSource, /calcColumnLabel: 'Расчёт'/);
  assert.doesNotMatch(detailSource, />Key</);
  assert.doesNotMatch(detailSource, />Calc</);
});
