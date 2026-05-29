import type { Monaco } from '@monaco-editor/react';
import type { editor as MonacoEditorNS } from 'monaco-editor';
import type * as monaco from 'monaco-editor';

export type ITextModel = MonacoEditorNS.ITextModel;
export type Position = monaco.Position;

export type CsxDiagnostic = {
  code: string;
  message: string;
  severity: 'Error' | 'Warning' | 'Information' | 'Hint' | number;
  startLine: number;
  startCharacter: number;
  endLine: number;
  endCharacter: number;
  documentPath?: string | null;
};

export type CsxCompletionItem = {
  label: string;
  sortText?: string;
  filterText?: string;
  insertText?: string;
  detail?: string;
  documentation?: string;
  kind?: string;
};

export type CsxHover = {
  signature: string;
  documentation?: string;
};

export type CsxSignatureHelp = {
  signatures: Array<{
    label: string;
    documentation?: string;
    parameters: Array<{ label: string; documentation?: string }>;
  }>;
  activeSignature: number;
  activeParameter: number;
};

export type CsxTextRange = {
  startLine: number;
  startCharacter: number;
  endLine: number;
  endCharacter: number;
};

export type CsxLocation = {
  documentPath: string;
  range: CsxTextRange;
  sourceCode?: string | null;
};

export type CsxTextEdit = {
  range: CsxTextRange;
  newText: string;
};

export type CsxWorkspaceEdit = {
  documentEdits: Array<{
    documentPath: string;
    edits: CsxTextEdit[];
  }>;
};

export type CsxFoldingRange = {
  startLine: number;
  endLine: number;
  kind?: string;
};

export type CsxSemanticTokens = {
  legend: {
    tokenTypes: string[];
    tokenModifiers: string[];
  };
  data: number[];
};

export type CsxCallHierarchyItem = {
  name: string;
  kind: string;
  detail?: string;
  documentPath: string;
  range: CsxTextRange;
  selectionRange: CsxTextRange;
};

export type CsxCallHierarchyIncomingCall = {
  from: CsxCallHierarchyItem;
  fromRanges: CsxTextRange[];
};

export type CsxCallHierarchyOutgoingCall = {
  to: CsxCallHierarchyItem;
  fromRanges: CsxTextRange[];
};

export interface MonacoCsxEditorProps {
  value: string;
  onChange: (value: string) => void;
  documentUri?: string;
  minimap?: boolean;
  readOnly?: boolean;
}

export type MonacoApi = Monaco;
export type MonacoEditor = MonacoEditorNS.IStandaloneCodeEditor;
export type MonacoMarkerData = MonacoEditorNS.IMarkerData;
export type MonacoDisposable = monaco.IDisposable;
