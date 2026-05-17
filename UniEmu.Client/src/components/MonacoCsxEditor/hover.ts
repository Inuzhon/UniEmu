import { MONACO_LANGUAGE_ID } from './constants';
import { hoverContents } from './markdown';
import { request } from './request';
import { wordRange } from './ranges';
import type { CsxHover, ITextModel, MonacoApi, Position } from './types';

export function registerHoverProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerHoverProvider(MONACO_LANGUAGE_ID, {
    provideHover: async (model: ITextModel, position: Position) => {
      const serverHover = await request<CsxHover>('/api/intellisense/csharp/hover', model, position);
      if (serverHover?.signature) {
        return {
          range: wordRange(monacoApi, model, position),
          contents: hoverContents(serverHover.signature, serverHover.documentation),
        };
      }

      return null;
    },
  });
}
