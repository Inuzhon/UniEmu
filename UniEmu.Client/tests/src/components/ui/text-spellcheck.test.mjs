import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const uiDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'src', 'components', 'ui');
const srcDir = join(uiDir, '..', '..');

test('shared text fields enable Russian spellcheck by default', async () => {
  const [input, textarea] = await Promise.all([
    readFile(join(uiDir, 'input.tsx'), 'utf8'),
    readFile(join(uiDir, 'textarea.tsx'), 'utf8'),
  ]);

  assert.match(input, /spellCheck=\{spellCheck \?\? shouldSpellCheckInput\(type\)\}/);
  assert.match(input, /lang=\{lang \?\? 'ru'\}/);
  assert.match(input, /function shouldSpellCheckInput/);
  assert.match(input, /technicalInputTypes\.has\(type\)/);

  assert.match(textarea, /spellCheck=\{spellCheck \?\? true\}/);
  assert.match(textarea, /lang=\{lang \?\? 'ru'\}/);
});

test('technical editors and identifiers opt out of text spellcheck', async () => {
  const [cncViewer, createScriptModal, storageFileRow] = await Promise.all([
    readFile(join(srcDir, 'routes', 'cnc', 'components', 'CncViewer.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'scripts', 'components', 'CreateScriptModal.tsx'), 'utf8'),
    readFile(join(srcDir, 'components', 'storage', 'StorageFileRow.tsx'), 'utf8'),
  ]);

  assert.match(cncViewer, /<Textarea[\s\S]*spellCheck=\{false\}/);
  assert.match(createScriptModal, /<Input[\s\S]*spellCheck=\{false\}[\s\S]*className="font-mono"/);
  assert.match(storageFileRow, /<input[\s\S]*spellCheck=\{false\}/);
});
