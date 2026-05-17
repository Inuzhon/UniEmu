import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('special parameter select renders human-readable labels', async () => {
  const componentSource = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'AddTagDrawer.tsx'), 'utf8');
  const typesSource = await readFile(join(dirname(fileURLToPath(import.meta.url)), '../../../types/uniemu.ts'), 'utf8');

  assert.match(typesSource, /SPECIAL_PARAMETER_OPTIONS/);
  assert.match(typesSource, /value: 'PrgName', label: 'Имя УП'/);
  assert.match(typesSource, /value: 'Subprogram', label: 'Имя подпрограммы'/);
  assert.match(componentSource, /SPECIAL_PARAMETER_OPTIONS\.map\(\(option\) =>/);
  assert.match(componentSource, /value=\{option\.value\}/);
  assert.match(componentSource, /\{option\.label\}/);
});
