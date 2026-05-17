import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const componentDir = join(dirname(fileURLToPath(import.meta.url)), 'MonacoCsxEditor');
const readComponentFile = (fileName) => readFile(join(componentDir, fileName), 'utf8');

test('csx facade uses Monaco csharp language id for editor models and providers', async () => {
  const editorSource = await readComponentFile('MonacoCsxEditor.tsx');
  const constantsSource = await readComponentFile('constants.ts');
  const completionSource = await readComponentFile('completion.ts');

  assert.match(constantsSource, /MONACO_LANGUAGE_ID = 'csharp'/);
  assert.match(editorSource, /defaultLanguage=\{MONACO_LANGUAGE_ID\}/);
  assert.match(editorSource, /language=\{MONACO_LANGUAGE_ID\}/);
  assert.match(editorSource, /setLanguageId\?\.\(MONACO_LANGUAGE_ID\)/);
  assert.match(completionSource, /registerCompletionItemProvider\(MONACO_LANGUAGE_ID/);
  assert.doesNotMatch(`${editorSource}\n${completionSource}`, /register\(\{\s*id: 'csx'/);
});

test('csx documents keep csx uri facade while using csharp language id', async () => {
  const source = await readComponentFile('MonacoCsxEditor.tsx');

  assert.match(source, /monacoApi\.Uri\.parse\(nextDocumentUri\)/);
  assert.match(source, /monacoApi\.editor\.createModel\(nextValue, MONACO_LANGUAGE_ID, uri\)/);
  assert.match(source, /editorRef\.current\?\.setModel\(model\)/);
  assert.match(source, /setLanguageId\?\.\(MONACO_LANGUAGE_ID\)/);
  assert.doesNotMatch(source, /setLanguageId\('csx'\)/);
});

test('editor uses REST intellisense instead of WebSocket LSP transport', async () => {
  const diagnosticsSource = await readComponentFile('diagnostics.ts');
  const completionSource = await readComponentFile('completion.ts');
  const hoverSource = await readComponentFile('hover.ts');
  const signatureHelpSource = await readComponentFile('signatureHelp.ts');
  const combinedSource = [diagnosticsSource, completionSource, hoverSource, signatureHelpSource].join('\n');

  assert.match(diagnosticsSource, /\/api\/intellisense\/csharp\/diagnostics/);
  assert.match(completionSource, /\/api\/intellisense\/csharp\/completions/);
  assert.match(hoverSource, /\/api\/intellisense\/csharp\/hover/);
  assert.match(signatureHelpSource, /\/api\/intellisense\/csharp\/signature-help/);
  assert.doesNotMatch(combinedSource, /activateCsxLanguageClient/);
});

test('csx intellisense registers advanced Monaco language providers', async () => {
  const registerSource = await readComponentFile('registerCsxIntellisense.ts');
  const providerSources = await Promise.all(
    [
      'navigation.ts',
      'references.ts',
      'implementation.ts',
      'rename.ts',
      'formatting.ts',
      'folding.ts',
      'semanticTokens.ts',
    ].map(readComponentFile)
  );
  const combinedSource = [registerSource, ...providerSources].join('\n');

  assert.match(combinedSource, /registerDefinitionProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerTypeDefinitionProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerReferenceProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerImplementationProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerRenameProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerDocumentFormattingEditProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerDocumentRangeFormattingEditProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerFoldingRangeProvider\(MONACO_LANGUAGE_ID/);
  assert.match(combinedSource, /registerDocumentSemanticTokensProvider\(MONACO_LANGUAGE_ID/);
  assert.doesNotMatch(registerSource, /registerCallHierarchyProvider/);
});

test('csx intellisense keeps provider disposables to avoid duplicate registrations', async () => {
  const registerSource = await readComponentFile('registerCsxIntellisense.ts');
  const completionSource = await readComponentFile('completion.ts');

  assert.match(registerSource, /dispose\(\)/);
  assert.match(registerSource, /globalThis/);
  assert.match(completionSource, /return monacoApi\.languages\.registerCompletionItemProvider/);
});

test('editor does not mix stale local host api completions with server intellisense', async () => {
  const sources = await Promise.all(
    [
      'MonacoCsxEditor.tsx',
      'completion.ts',
      'hover.ts',
      'signatureHelp.ts',
      'registerCsxIntellisense.ts',
    ].map(readComponentFile)
  );
  const editorSource = sources.join('\n');

  assert.doesNotMatch(editorSource, /Tag\["\$\{1:tagName\}"\]/);
  assert.doesNotMatch(editorSource, /Tags\.SetStatic/);
  assert.doesNotMatch(editorSource, /LogWarn/);
  assert.doesNotMatch(editorSource, /LogError/);
  assert.doesNotMatch(editorSource, /Random\(\$\{1:min\}/);
  assert.doesNotMatch(editorSource, /\.\.\.localCompletionItems/);
  assert.doesNotMatch(editorSource, /localHoverItem/);
  assert.doesNotMatch(editorSource, /localSignatureHelp/);
});

test('csx completion keeps basic snippets in a dedicated snippet source', async () => {
  const completionSource = await readComponentFile('completion.ts');
  const snippetsSource = await readComponentFile('snippets.ts');

  assert.match(completionSource, /createCsxSnippetCompletions/);
  assert.match(completionSource, /triggerCharacters: \['\.', '#', ' ', '"', '\/'\]/);
  assert.match(snippetsSource, /CompletionItemKind\.Snippet/);
  assert.match(snippetsSource, /CompletionItemInsertTextRule\.InsertAsSnippet/);
  assert.match(snippetsSource, /label: 'if'/);
  assert.match(snippetsSource, /label: '\/\/\/'/);
  assert.match(snippetsSource, /label: '\/\* \*\/'/);
});
