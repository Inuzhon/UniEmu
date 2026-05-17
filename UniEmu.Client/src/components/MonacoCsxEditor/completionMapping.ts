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
  const kindByServerKind: Record<string, languages.CompletionItemKind> = {
    class: monacoApi.languages.CompletionItemKind.Class,
    constant: monacoApi.languages.CompletionItemKind.Constant,
    enum: monacoApi.languages.CompletionItemKind.Enum,
    enummember: monacoApi.languages.CompletionItemKind.EnumMember,
    event: monacoApi.languages.CompletionItemKind.Event,
    field: monacoApi.languages.CompletionItemKind.Field,
    function: monacoApi.languages.CompletionItemKind.Function,
    interface: monacoApi.languages.CompletionItemKind.Interface,
    keyword: monacoApi.languages.CompletionItemKind.Keyword,
    method: monacoApi.languages.CompletionItemKind.Method,
    module: monacoApi.languages.CompletionItemKind.Module,
    operator: monacoApi.languages.CompletionItemKind.Operator,
    property: monacoApi.languages.CompletionItemKind.Property,
    reference: monacoApi.languages.CompletionItemKind.Reference,
    struct: monacoApi.languages.CompletionItemKind.Struct,
    typeparameter: monacoApi.languages.CompletionItemKind.TypeParameter,
    variable: monacoApi.languages.CompletionItemKind.Variable,
  };

  return kindByServerKind[kind?.toLowerCase() ?? ''] ?? monacoApi.languages.CompletionItemKind.Text;
}
