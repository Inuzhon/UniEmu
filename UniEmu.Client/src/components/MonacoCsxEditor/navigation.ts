import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import { toMonacoLocation } from './ranges';
import type { CsxLocation, ITextModel, MonacoApi, Position } from './types';

export function registerNavigationProviders(monacoApi: MonacoApi) {
  return [
    monacoApi.languages.registerDefinitionProvider(MONACO_LANGUAGE_ID, {
    provideDefinition: async (model: ITextModel, position: Position) => {
      const locations = await request<CsxLocation[]>('/api/intellisense/csharp/definition', model, position);
      return (locations ?? []).map((location) => toMonacoLocation(monacoApi, model, location));
    },
  }),

    monacoApi.languages.registerTypeDefinitionProvider(MONACO_LANGUAGE_ID, {
    provideTypeDefinition: async (model: ITextModel, position: Position) => {
      const locations = await request<CsxLocation[]>('/api/intellisense/csharp/type-definition', model, position);
      return (locations ?? []).map((location) => toMonacoLocation(monacoApi, model, location));
    },
  }),
  ];
}
