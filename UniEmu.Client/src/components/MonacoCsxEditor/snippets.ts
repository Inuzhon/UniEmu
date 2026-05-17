import type { IRange, languages } from 'monaco-editor';
import type { MonacoApi } from './types';

type CsxSnippet = {
  label: string;
  insertText: string;
  detail: string;
  documentation?: string;
};

const csxSnippets: CsxSnippet[] = [
  {
    label: 'if',
    insertText: 'if (${1:condition}) {\n\t$0\n}',
    detail: 'if statement',
  },
  {
    label: 'ifelse',
    insertText: 'if (${1:condition}) {\n\t${2}\n} else {\n\t$0\n}',
    detail: 'if/else statement',
  },
  {
    label: 'for',
    insertText: 'for (var ${1:i} = 0; ${1:i} < ${2:count}; ${1:i}++) {\n\t$0\n}',
    detail: 'for loop',
  },
  {
    label: 'foreach',
    insertText: 'foreach (var ${1:item} in ${2:items}) {\n\t$0\n}',
    detail: 'foreach loop',
  },
  {
    label: 'while',
    insertText: 'while (${1:condition}) {\n\t$0\n}',
    detail: 'while loop',
  },
  {
    label: 'try',
    insertText: 'try {\n\t${1}\n} catch (${2:Exception} ${3:ex}) {\n\t$0\n}',
    detail: 'try/catch block',
  },
  {
    label: 'tryfinally',
    insertText: 'try {\n\t${1}\n} finally {\n\t$0\n}',
    detail: 'try/finally block',
  },
  {
    label: 'using',
    insertText: 'using (${1:var resource = expression}) {\n\t$0\n}',
    detail: 'using block',
  },
  {
    label: 'return',
    insertText: 'return ${1:value};',
    detail: 'return statement',
  },
  {
    label: '///',
    insertText: '/// <summary>\n/// ${1:Description}\n/// </summary>',
    detail: 'XML documentation comment',
  },
  {
    label: '/* */',
    insertText: '/*\n * ${1:Comment}\n */',
    detail: 'block comment',
  },
];

export function createCsxSnippetCompletions(
  monacoApi: MonacoApi,
  range: IRange
): languages.CompletionItem[] {
  return csxSnippets.map((snippet) => ({
    label: snippet.label,
    kind: monacoApi.languages.CompletionItemKind.Snippet,
    insertText: snippet.insertText,
    insertTextRules: monacoApi.languages.CompletionItemInsertTextRule.InsertAsSnippet,
    detail: snippet.detail,
    documentation: snippet.documentation,
    range,
    sortText: `01_snippet_${snippet.label}`,
    filterText: snippet.label,
  }));
}
