import { MONACO_LANGUAGE_ID } from './constants';
import { isNavigationEnabledForModel } from './modelNavigation';
import { request } from './request';
import { toMonacoLocation } from './ranges';
import type { CsxLocation, ITextModel, MonacoApi, Position } from './types';

export function registerReferencesProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerReferenceProvider(MONACO_LANGUAGE_ID, {
    provideReferences: async (model: ITextModel, position: Position) => {
      if (!isNavigationEnabledForModel(model)) return [];

      const locations = await request<CsxLocation[]>('/api/intellisense/csharp/references', model, position, {
        includeDeclaration: true,
      });
      return (locations ?? []).map((location) => toMonacoLocation(monacoApi, model, location));
    },
  });
}
