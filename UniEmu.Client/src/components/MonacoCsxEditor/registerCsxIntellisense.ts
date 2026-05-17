import { registerCompletionProvider } from './completion';
import { registerFoldingRangeProvider } from './folding';
import { registerFormattingProviders } from './formatting';
import { registerHoverProvider } from './hover';
import { registerImplementationProvider } from './implementation';
import { registerNavigationProviders } from './navigation';
import { registerReferencesProvider } from './references';
import { registerRenameProvider } from './rename';
import { registerSemanticTokensProvider } from './semanticTokens';
import { registerSignatureHelpProvider } from './signatureHelp';
import type { MonacoApi, MonacoDisposable } from './types';

const REGISTRATION_KEY = '__uniemuCsxIntellisenseDisposables';

type CsxGlobal = typeof globalThis & {
  [REGISTRATION_KEY]?: MonacoDisposable[];
};

export function registerCsxIntellisense(monacoApi: MonacoApi) {
  const csxGlobal = globalThis as CsxGlobal;
  csxGlobal[REGISTRATION_KEY]?.forEach((disposable) => disposable.dispose());

  csxGlobal[REGISTRATION_KEY] = [
    registerCompletionProvider(monacoApi),
    registerHoverProvider(monacoApi),
    registerSignatureHelpProvider(monacoApi),
    ...registerNavigationProviders(monacoApi),
    registerReferencesProvider(monacoApi),
    registerImplementationProvider(monacoApi),
    registerRenameProvider(monacoApi),
    ...registerFormattingProviders(monacoApi),
    registerFoldingRangeProvider(monacoApi),
    registerSemanticTokensProvider(monacoApi),
  ];
}
