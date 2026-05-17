import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import type { CsxSemanticTokens, ITextModel, MonacoApi } from './types';

const legend = {
  tokenTypes: ['class', 'struct', 'enum', 'interface', 'method', 'property', 'field', 'variable', 'parameter', 'keyword', 'number', 'string', 'comment'],
  tokenModifiers: [],
};

export function registerSemanticTokensProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerDocumentSemanticTokensProvider(MONACO_LANGUAGE_ID, {
    getLegend: () => legend,
    provideDocumentSemanticTokens: async (model: ITextModel) => {
      const tokens = await request<CsxSemanticTokens>('/api/intellisense/csharp/semantic-tokens', model);
      return {
        data: new Uint32Array(tokens?.data ?? []),
        resultId: undefined,
      };
    },
    releaseDocumentSemanticTokens: () => {},
  });
}
