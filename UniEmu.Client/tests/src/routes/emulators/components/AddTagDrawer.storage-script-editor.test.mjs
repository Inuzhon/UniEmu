import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag drawer can edit the selected script from storage', async () => {
  const componentDir = join(
    dirname(fileURLToPath(import.meta.url)),
    '..',
    '..',
    '..',
    '..',
    '..',
    'src',
    'routes',
    'emulators',
    'components'
  );
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const scriptSectionSource = await readFile(
    join(componentDir, 'tag-editor/TagScriptSection.tsx'),
    'utf8'
  );
  const editorDrawerSource = await readFile(
    join(componentDir, 'tag-editor/InlineScriptEditorDrawer.tsx'),
    'utf8'
  );
  const localizationSource = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(drawerSource, /const updateScript = useUniEmuStore\(\(s\) => s\.updateScript\)/);
  assert.match(drawerSource, /const selectedStorageScript = useMemo/);
  assert.match(drawerSource, /openStorageScriptEditor/);
  assert.match(drawerSource, /applyStorageScriptDraft/);
  assert.match(
    drawerSource,
    /await updateScript\(selectedStorageScript\.id, storageScriptEditorDraft\)/
  );
  assert.match(drawerSource, /buildCsxDocumentUri\(\{\s*id: selectedStorageScript\.id/);

  assert.match(scriptSectionSource, /selectedScript\?: ScriptFile \| null/);
  assert.match(scriptSectionSource, /onOpenStorageScriptEditor: \(\) => void/);
  assert.match(scriptSectionSource, /selectedScript && \(/);
  assert.match(scriptSectionSource, /storageScriptEditButtonLabel/);

  assert.match(editorDrawerSource, /title: string/);
  assert.match(editorDrawerSource, /applyButtonLabel: string/);

  assert.match(localizationSource, /storageScriptEditButtonLabel:/);
  assert.match(localizationSource, /saveScriptButtonLabel:/);
  assert.match(localizationSource, /storageScriptEditorTitle:/);
});
