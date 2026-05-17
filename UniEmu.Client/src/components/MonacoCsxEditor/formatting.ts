import type { IRange } from 'monaco-editor';
import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import { toMonacoTextEdit, toServerRange } from './ranges';
import type { CsxTextEdit, ITextModel, MonacoApi } from './types';

export function registerFormattingProviders(monacoApi: MonacoApi) {
  return [
    monacoApi.languages.registerDocumentFormattingEditProvider(MONACO_LANGUAGE_ID, {
    provideDocumentFormattingEdits: async (model: ITextModel) => {
      const edits = await request<CsxTextEdit[]>('/api/intellisense/csharp/format', model);
      return (edits ?? []).map((edit) => toMonacoTextEdit(monacoApi, edit));
    },
  }),

    monacoApi.languages.registerDocumentRangeFormattingEditProvider(MONACO_LANGUAGE_ID, {
    provideDocumentRangeFormattingEdits: async (model: ITextModel, range: IRange) => {
      const edits = await request<CsxTextEdit[]>('/api/intellisense/csharp/format-range', model, undefined, {
        range: toServerRange(range),
      });
      return (edits ?? []).map((edit) => toMonacoTextEdit(monacoApi, edit));
    },
  }),
  ];
}
