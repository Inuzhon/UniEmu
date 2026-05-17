import type { languages } from 'monaco-editor';
import { MONACO_LANGUAGE_ID } from './constants';
import { dedupeCompletionItems, mapServerCompletion } from './completionMapping';
import { request } from './request';
import { completionRange } from './ranges';
import { createCsxSnippetCompletions } from './snippets';
import type { CsxCompletionItem, ITextModel, MonacoApi, Position } from './types';

export function registerCompletionProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerCompletionItemProvider(MONACO_LANGUAGE_ID, {
    triggerCharacters: ['.', '#', ' ', '"', '/'],
    provideCompletionItems: async (
      model: ITextModel,
      position: Position
    ): Promise<languages.CompletionList> => {
      const range = completionRange(model, position);
      const serverItems = await request<CsxCompletionItem[]>('/api/intellisense/csharp/completions', model, position);
      const suggestions = [
        ...(serverItems ?? []).map((item) => mapServerCompletion(monacoApi, item, range)),
        ...createCsxSnippetCompletions(monacoApi, range),
      ];

      return { suggestions: dedupeCompletionItems(suggestions) };
    },
  });
}
