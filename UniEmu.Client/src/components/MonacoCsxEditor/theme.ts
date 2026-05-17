import type { MonacoApi } from './types';

let themeRegistered = false;

export function registerTheme(monacoApi: MonacoApi) {
  if (themeRegistered) return;
  themeRegistered = true;

  monacoApi.editor.defineTheme('uniemu-dark', {
    base: 'vs-dark',
    inherit: true,
    rules: [
      { token: 'comment', foreground: '5b6878', fontStyle: 'italic' },
      { token: 'keyword', foreground: '7dd3fc' },
      { token: 'string', foreground: 'a7f3d0' },
      { token: 'number', foreground: 'fbbf24' },
      { token: 'type', foreground: 'c4b5fd' },
      { token: 'identifier', foreground: 'e2e8f0' },
      { token: 'delimiter', foreground: '94a3b8' },
    ],
    colors: {
      'editor.background': '#0b1220',
      'editor.foreground': '#e2e8f0',
      'editorLineNumber.foreground': '#475569',
      'editorLineNumber.activeForeground': '#7dd3fc',
      'editor.lineHighlightBackground': '#13203a',
      'editor.selectionBackground': '#1e3a5f',
      'editorCursor.foreground': '#7dd3fc',
      'editorIndentGuide.background': '#1e293b',
      'editorIndentGuide.activeBackground': '#334155',
      'editorBracketMatch.background': '#1e3a5f',
      'editorBracketMatch.border': '#7dd3fc',
    },
  });

  monacoApi.editor.defineTheme('uniemu-light', {
    base: 'vs',
    inherit: true,
    rules: [
      { token: 'comment', foreground: '8b92a0', fontStyle: 'italic' },
      { token: 'keyword', foreground: '0f5c8b' },
      { token: 'string', foreground: '106b46' },
      { token: 'number', foreground: '7f5a1a' },
      { token: 'type', foreground: '5f4d8b' },
      { token: 'identifier', foreground: '333333' },
      { token: 'delimiter', foreground: '666666' },
    ],
    colors: {
      'editor.background': '#faf8f6',
      'editor.foreground': '#1a1a1a',
      'editorLineNumber.foreground': '#a0a0a0',
      'editorLineNumber.activeForeground': '#0f5c8b',
      'editor.lineHighlightBackground': '#f0ede8',
      'editor.selectionBackground': '#c8dae6',
      'editorCursor.foreground': '#0f5c8b',
      'editorIndentGuide.background': '#e8e6e4',
      'editorIndentGuide.activeBackground': '#d4d2d0',
      'editorBracketMatch.background': '#c8dae6',
      'editorBracketMatch.border': '#0f5c8b',
    },
  });
}
