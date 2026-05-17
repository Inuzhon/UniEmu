import { useEffect, useRef } from 'react';
import Editor, { type Monaco, type OnMount } from '@monaco-editor/react';
import { localization } from '@/localization';
import { bindCsxDiagnostics } from './diagnostics';
import { MONACO_LANGUAGE_ID } from './constants';
import { monaco } from './monacoEnvironment';
import { registerCsxIntellisense } from './registerCsxIntellisense';
import { registerTheme } from './theme';
import type { ITextModel, MonacoCsxEditorProps, MonacoEditor } from './types';

export const CsxLanguageAPI = {
  getHostAPI: () => [],
  getDirectives: () => [],
  getKeywords: () => [],
};

export function MonacoCsxEditor({
  value,
  onChange,
  documentUri,
  minimap = true,
  readOnly,
}: MonacoCsxEditorProps) {
  const editorRef = useRef<MonacoEditor | null>(null);
  const monacoRef = useRef<Monaco | null>(null);
  const modelRef = useRef<ITextModel | null>(null);
  const diagnosticsRef = useRef<(() => void) | null>(null);

  const isDarkTheme = !document.documentElement.classList.contains('light');
  const currentTheme = isDarkTheme ? 'uniemu-dark' : 'uniemu-light';

  const attachModel = (nextDocumentUri: string, nextValue: string, monacoApi: Monaco) => {
    const uri = monacoApi.Uri.parse(nextDocumentUri);
    const existing = monacoApi.editor.getModel(uri);
    const model = existing ?? monacoApi.editor.createModel(nextValue, MONACO_LANGUAGE_ID, uri);

    if (model.getLanguageId() !== MONACO_LANGUAGE_ID) {
      monacoApi.editor.setModelLanguage(model, MONACO_LANGUAGE_ID);
      (model as ITextModel & { setLanguageId?: (languageId: string) => void }).setLanguageId?.(MONACO_LANGUAGE_ID);
    }

    if (model.getValue() !== nextValue) {
      model.setValue(nextValue);
    }

    modelRef.current = model;
    editorRef.current?.setModel(model);
  };

  const handleMount: OnMount = (editor, monacoApi) => {
    editorRef.current = editor;
    monacoRef.current = monacoApi;
    registerCsxIntellisense(monacoApi);
    registerTheme(monacoApi);
    monacoApi.editor.setTheme(currentTheme);

    if (documentUri) {
      attachModel(documentUri, value, monacoApi);
    }

    diagnosticsRef.current?.();
    diagnosticsRef.current = bindCsxDiagnostics(editor, monacoApi);
  };

  useEffect(() => {
    const observer = new MutationObserver(() => {
      const isDark = !document.documentElement.classList.contains('light');
      monaco.editor.setTheme(isDark ? 'uniemu-dark' : 'uniemu-light');
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => {
      observer.disconnect();
      diagnosticsRef.current?.();
      diagnosticsRef.current = null;
      modelRef.current?.dispose();
      modelRef.current = null;
      editorRef.current?.dispose();
      editorRef.current = null;
      monacoRef.current = null;
    };
  }, []);

  useEffect(() => {
    const monacoApi = monacoRef.current;
    if (!documentUri || !monacoApi) return;
    attachModel(documentUri, value, monacoApi);
  }, [documentUri, value]);

  return (
    <Editor
      height="100%"
      defaultLanguage={MONACO_LANGUAGE_ID}
      language={MONACO_LANGUAGE_ID}
      path={documentUri}
      theme={currentTheme}
      value={value}
      onChange={(v) => onChange(v ?? '')}
      onMount={handleMount}
      loading={
        <div className="flex h-full items-center justify-center font-mono text-xs text-muted-foreground">
          {localization.components.monacoCsxEditor.loadingLabel}
        </div>
      }
      options={{
        readOnly,
        fontFamily: "'JetBrains Mono', ui-monospace, SFMono-Regular, Menlo, monospace",
        fontSize: 13,
        lineHeight: 22,
        minimap: {
          enabled: minimap,
          renderCharacters: false,
          maxColumn: 80,
        },
        fixedOverflowWidgets: true,
        smoothScrolling: true,
        cursorBlinking: 'smooth',
        cursorSmoothCaretAnimation: 'on',
        renderLineHighlight: 'all',
        scrollBeyondLastLine: false,
        automaticLayout: true,
        tabSize: 4,
        insertSpaces: true,
        bracketPairColorization: { enabled: true },
        guides: { indentation: true, bracketPairs: true },
        suggest: {
          showWords: false,
          showStatusBar: true,
          shareSuggestSelections: true,
        },
        quickSuggestions: {
          other: true,
          comments: false,
          strings: false,
        },
        parameterHints: {
          enabled: true,
        },
        suggestOnTriggerCharacters: true,
        acceptSuggestionOnEnter: 'on',
        padding: { top: 12, bottom: 12 },
      }}
    />
  );
}
