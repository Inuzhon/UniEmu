import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const routesDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src', 'routes');
const srcDir = join(routesDir, '..');

test('cnc text editor uses the same themed editor surface tokens as script preview', async () => {
  const [styles, cncViewer, scriptEditor] = await Promise.all([
    readFile(join(srcDir, 'styles.css'), 'utf8'),
    readFile(join(routesDir, 'cnc', 'components', 'CncViewer.tsx'), 'utf8'),
    readFile(join(routesDir, 'scripts', 'components', 'ScriptEditor.tsx'), 'utf8'),
  ]);

  assert.match(styles, /--editor-background:/);
  assert.match(styles, /--editor-foreground:/);
  assert.match(styles, /\.light\s*\{[\s\S]*--editor-background:/);
  assert.match(styles, /\.light\s*\{[\s\S]*--editor-foreground:/);

  assert.match(styles, /\.editor-surface\s*\{[\s\S]*background-color: var\(--editor-background\);/);
  assert.match(styles, /\.editor-surface\s*\{[\s\S]*color: var\(--editor-foreground\);/);
  assert.match(styles, /\.editor-text\s*\{[\s\S]*color: var\(--editor-foreground\);/);

  assert.match(cncViewer, /editor-surface/);
  assert.match(cncViewer, /editor-text/);
  assert.doesNotMatch(
    cncViewer,
    /bg-background p-4 font-mono text-\[13px\] leading-6 text-foreground/
  );

  assert.match(scriptEditor, /editor-surface/);
  assert.match(scriptEditor, /readOnly=\{!editing\}/);
  assert.doesNotMatch(scriptEditor, /<pre className=/);
});

test('script editor starts read-only and opens Monaco editing from an edit button', async () => {
  const scriptEditor = await readFile(
    join(routesDir, 'scripts', 'components', 'ScriptEditor.tsx'),
    'utf8'
  );

  assert.match(scriptEditor, /const \[editing, setEditing\] = useState\(false\)/);
  assert.match(scriptEditor, /<Pencil className="h-3\.5 w-3\.5" \/>/);
  assert.match(scriptEditor, /const startEditing = \(\) => \{/);
  assert.match(scriptEditor, /onClick=\{startEditing\}/);
  assert.match(scriptEditor, /<MonacoCsxEditor/);
  assert.match(scriptEditor, /readOnly=\{!editing\}/);
  assert.doesNotMatch(scriptEditor, /<pre className=/);
  assert.match(scriptEditor, /setDraft\(file\.content\);[\s\S]*setEditing\(false\);/);
  assert.match(scriptEditor, /dirty = editing && draft !== file\.content/);
});

test('script editor save error remains readable on the light theme', async () => {
  const scriptEditor = await readFile(
    join(routesDir, 'scripts', 'components', 'ScriptEditor.tsx'),
    'utf8'
  );

  assert.match(scriptEditor, /bg-destructive\/10/);
  assert.match(scriptEditor, /text-destructive/);
  assert.doesNotMatch(scriptEditor, /text-destructive-foreground/);
});

test('scripts page guards navigation when the selected script has unsaved changes', async () => {
  const scriptsPage = await readFile(
    join(routesDir, 'scripts', 'components', 'ScriptsPage.tsx'),
    'utf8'
  );
  const scriptEditor = await readFile(
    join(routesDir, 'scripts', 'components', 'ScriptEditor.tsx'),
    'utf8'
  );

  assert.match(scriptEditor, /onDirtyChange: \(dirty: boolean\) => void/);
  assert.match(scriptEditor, /onDirtyChange\(dirty\)/);
  assert.match(scriptsPage, /const \[selectedDirty, setSelectedDirty\] = useState\(false\)/);
  assert.match(scriptsPage, /confirmDiscardUnsavedChanges/);
  assert.match(scriptsPage, /selectScript\(sc\.id\)/);
  assert.doesNotMatch(scriptsPage, /onSelect=\{\(\) => setSelectedId\(sc\.id\)\}/);
  assert.match(scriptsPage, /onDirtyChange=\{setSelectedDirty\}/);
  assert.match(scriptsPage, /beforeunload/);
});
