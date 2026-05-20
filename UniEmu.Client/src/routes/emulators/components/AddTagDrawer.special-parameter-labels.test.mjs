import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('special parameter select renders human-readable labels', async () => {
  const componentSource = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'tag-editor/TagBasicsSection.tsx'), 'utf8');
  const typesSource = await readFile(join(dirname(fileURLToPath(import.meta.url)), '../../../types/uniemu.ts'), 'utf8');
  const optionsSource = typesSource.match(/export const SPECIAL_PARAMETER_OPTIONS[\s\S]*?\n\];/)?.[0] ?? '';

  assert.match(typesSource, /SPECIAL_PARAMETER_OPTIONS/);
  assert.match(optionsSource, /value: 'PrgName', label: 'Имя УП'/);
  assert.match(optionsSource, /value: 'FrameNum', label: 'Номер кадра УП'/);
  assert.match(optionsSource, /value: 'FrameText', label: 'Текст кадра УП'/);
  assert.doesNotMatch(optionsSource, /value: 'Subprogram', label: 'Имя подпрограммы'/);
  assert.doesNotMatch(optionsSource, /value: 'PartCounter'/);
  assert.match(componentSource, /SPECIAL_PARAMETER_OPTIONS\.map\(\(option\) =>/);
  assert.match(componentSource, /value=\{option\.value\}/);
  assert.match(componentSource, /\{option\.label\}/);
});

test('tag trigger select hides cron option from drawer', async () => {
  const componentSource = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'tag-editor/TagTriggerSection.tsx'), 'utf8');

  assert.match(componentSource, /<SelectItem value="once">/);
  assert.match(componentSource, /<SelectItem value="interval">/);
  assert.doesNotMatch(componentSource, /<SelectItem value="cron">/);
});
