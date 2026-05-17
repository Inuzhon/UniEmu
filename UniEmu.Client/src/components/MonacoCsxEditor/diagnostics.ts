import { MARKER_OWNER } from './constants';
import { cancelRequestsForModel, request } from './request';
import type { CsxDiagnostic, MonacoApi, MonacoEditor, MonacoMarkerData } from './types';

export function bindCsxDiagnostics(editor: MonacoEditor, monacoApi: MonacoApi) {
  let timer: ReturnType<typeof setTimeout> | undefined;
  let disposed = false;

  const update = () => {
    if (disposed) return;

    if (timer) clearTimeout(timer);
    timer = setTimeout(async () => {
      const model = editor.getModel();
      if (!model || disposed) return;

      const diagnostics = await request<CsxDiagnostic[]>('/api/intellisense/csharp/diagnostics', model);
      if (disposed) return;

      monacoApi.editor.setModelMarkers(
        model,
        MARKER_OWNER,
        (diagnostics ?? []).map((diagnostic) => mapDiagnostic(monacoApi, diagnostic))
      );
    }, 350);
  };

  const subscription = editor.onDidChangeModelContent(update);
  const modelSubscription = editor.onDidChangeModel(update);
  update();

  return () => {
    disposed = true;
    if (timer) clearTimeout(timer);
    subscription.dispose();
    modelSubscription.dispose();
    const model = editor.getModel();
    if (model) {
      cancelRequestsForModel(model);
      monacoApi.editor.setModelMarkers(model, MARKER_OWNER, []);
    }
  };
}

function mapDiagnostic(monacoApi: MonacoApi, diagnostic: CsxDiagnostic): MonacoMarkerData {
  const startLineNumber = diagnostic.startLine + 1;
  const startColumn = diagnostic.startCharacter + 1;
  const endLineNumber = diagnostic.endLine + 1;
  const endColumn = Math.max(diagnostic.endCharacter + 1, startColumn + 1);

  return {
    code: diagnostic.code,
    message: diagnostic.message,
    severity: mapSeverity(monacoApi, diagnostic.severity),
    startLineNumber,
    startColumn,
    endLineNumber: Math.max(endLineNumber, startLineNumber),
    endColumn,
  };
}

function mapSeverity(monacoApi: MonacoApi, severity: CsxDiagnostic['severity']) {
  if (severity === 'Error' || severity === 1) return monacoApi.MarkerSeverity.Error;
  if (severity === 'Warning' || severity === 2) return monacoApi.MarkerSeverity.Warning;
  if (severity === 'Information' || severity === 3) return monacoApi.MarkerSeverity.Info;
  return monacoApi.MarkerSeverity.Hint;
}
