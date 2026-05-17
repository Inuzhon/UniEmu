import type { IRange, languages } from 'monaco-editor';
import { markdown } from './markdown';
import type { CsxCompletionItem, MonacoApi } from './types';

export function mapServerCompletion(
  monacoApi: MonacoApi,
  item: CsxCompletionItem,
  range: IRange
): languages.CompletionItem {
  return {
    label: item.label,
    kind: mapCompletionKind(monacoApi, item.kind),
    insertText: item.insertText || item.label,
    detail: item.detail,
    documentation: markdown(item.documentation),
    range,
    sortText: item.sortText || `1_${item.label}`,
    filterText: item.filterText || item.label,
  };
}

export function dedupeCompletionItems(items: languages.CompletionItem[]) {
  const seen = new Set<string>();
  return items.filter((item) => {
    const key = String(item.label);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function mapCompletionKind(monacoApi: MonacoApi, kind?: string) {
  switch (kind?.toLowerCase()) {
    case 'method':
      return monacoApi.languages.CompletionItemKind.Method;
    case 'property':
      return monacoApi.languages.CompletionItemKind.Property;
    case 'class':
      return monacoApi.languages.CompletionItemKind.Class;
    case 'struct':
      return monacoApi.languages.CompletionItemKind.Struct;
    case 'enum':
      return monacoApi.languages.CompletionItemKind.Enum;
    case 'field':
      return monacoApi.languages.CompletionItemKind.Field;
    case 'keyword':
      return monacoApi.languages.CompletionItemKind.Keyword;
    case 'variable':
      return monacoApi.languages.CompletionItemKind.Variable;
    default:
      return monacoApi.languages.CompletionItemKind.Text;
  }
}
