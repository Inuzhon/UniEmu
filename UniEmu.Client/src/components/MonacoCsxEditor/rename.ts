import { MONACO_LANGUAGE_ID } from './constants';
import { request } from './request';
import { toModelUri, toMonacoRange } from './ranges';
import type { CsxWorkspaceEdit, ITextModel, MonacoApi, Position } from './types';

export function registerRenameProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerRenameProvider(MONACO_LANGUAGE_ID, {
    provideRenameEdits: async (model: ITextModel, position: Position, newName: string) => {
      const workspaceEdit = await request<CsxWorkspaceEdit>('/api/intellisense/csharp/rename', model, position, {
        newName,
      });

      return {
        edits: (workspaceEdit?.documentEdits ?? []).flatMap((documentEdit) =>
          documentEdit.edits.map((edit) => ({
            resource: toModelUri(monacoApi, model, documentEdit.documentPath),
            edit: {
              range: toMonacoRange(monacoApi, edit.range),
              text: edit.newText,
            },
          }))
        ),
      };
    },
  });
}
