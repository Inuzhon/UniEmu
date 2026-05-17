import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import type { CsxFoldingRange, ITextModel, MonacoApi } from './types';

export function registerFoldingRangeProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerFoldingRangeProvider(MONACO_LANGUAGE_ID, {
    provideFoldingRanges: async (model: ITextModel) => {
      const ranges = await request<CsxFoldingRange[]>('/api/intellisense/csharp/folding-ranges', model);
      return (ranges ?? []).map((range) => ({
        start: range.startLine + 1,
        end: range.endLine + 1,
        kind: range.kind === 'region' ? monacoApi.languages.FoldingRangeKind.Region : undefined,
      }));
    },
  });
}
