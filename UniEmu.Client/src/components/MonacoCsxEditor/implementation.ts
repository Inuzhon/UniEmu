import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import { toMonacoLocation } from './ranges';
import type { CsxLocation, ITextModel, MonacoApi, Position } from './types';

export function registerImplementationProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerImplementationProvider(MONACO_LANGUAGE_ID, {
    provideImplementation: async (model: ITextModel, position: Position) => {
      const locations = await request<CsxLocation[]>('/api/intellisense/csharp/implementation', model, position);
      return (locations ?? []).map((location) => toMonacoLocation(monacoApi, model, location));
    },
  });
}
