import type { languages } from 'monaco-editor';
import { MONACO_LANGUAGE_ID } from './constants';
import { markdown } from './markdown';
import { request } from './request';
import type { CsxSignatureHelp, ITextModel, MonacoApi, Position } from './types';

export function registerSignatureHelpProvider(monacoApi: MonacoApi) {
  return monacoApi.languages.registerSignatureHelpProvider(MONACO_LANGUAGE_ID, {
    signatureHelpTriggerCharacters: ['(', ','],
    signatureHelpRetriggerCharacters: [','],
    provideSignatureHelp: async (
      model: ITextModel,
      position: Position
    ): Promise<languages.SignatureHelpResult | null> => {
      const serverHelp = await request<CsxSignatureHelp>('/api/intellisense/csharp/signature-help', model, position);
      if (serverHelp?.signatures?.length) {
        return {
          value: {
            signatures: serverHelp.signatures.map((signature) => ({
              label: signature.label,
              documentation: markdown(signature.documentation),
              parameters: signature.parameters.map((parameter) => ({
                label: parameter.label,
                documentation: markdown(parameter.documentation),
              })),
            })),
            activeSignature: serverHelp.activeSignature,
            activeParameter: serverHelp.activeParameter,
          },
          dispose: () => {},
        };
      }

      return null;
    },
  });
}
