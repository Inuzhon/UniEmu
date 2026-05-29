import type { IRange } from 'monaco-editor';
import { MONACO_LANGUAGE_ID } from './constants';
import { setModelNavigationEnabled } from './modelNavigation';
import type { CsxLocation, CsxTextEdit, CsxTextRange, ITextModel, MonacoApi, Position } from './types';

export function completionRange(model: ITextModel, position: Position): IRange {
  const word = model.getWordUntilPosition(position);
  return {
    startLineNumber: position.lineNumber,
    endLineNumber: position.lineNumber,
    startColumn: word.startColumn,
    endColumn: word.endColumn,
  };
}

export function wordRange(monacoApi: MonacoApi, model: ITextModel, position: Position) {
  const word = model.getWordAtPosition(position);
  if (!word) {
    return new monacoApi.Range(position.lineNumber, position.column, position.lineNumber, position.column);
  }

  return new monacoApi.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
}

export function toMonacoRange(monacoApi: MonacoApi, range: CsxTextRange) {
  return new monacoApi.Range(
    range.startLine + 1,
    range.startCharacter + 1,
    range.endLine + 1,
    Math.max(range.endCharacter + 1, range.startCharacter + 2)
  );
}

export function toServerRange(range: IRange): CsxTextRange {
  return {
    startLine: range.startLineNumber - 1,
    startCharacter: range.startColumn - 1,
    endLine: range.endLineNumber - 1,
    endCharacter: range.endColumn - 1,
  };
}

export function toMonacoTextEdit(monacoApi: MonacoApi, edit: CsxTextEdit) {
  return {
    range: toMonacoRange(monacoApi, edit.range),
    text: edit.newText,
  };
}

export function toModelUri(monacoApi: MonacoApi, model: ITextModel, documentPath: string) {
  if (documentPath === model.uri.toString()) {
    return model.uri;
  }

  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(documentPath)) {
    return monacoApi.Uri.parse(documentPath);
  }

  return monacoApi.Uri.parse(`uniemu://scripts/${encodeURIComponent(documentPath)}`);
}

export function toMonacoLocation(monacoApi: MonacoApi, model: ITextModel, location: CsxLocation) {
  const uri = toModelUri(monacoApi, model, location.documentPath);
  hydrateNavigationModel(monacoApi, model, uri, location);

  return {
    uri,
    range: toMonacoRange(monacoApi, location.range),
  };
}

function hydrateNavigationModel(
  monacoApi: MonacoApi,
  currentModel: ITextModel,
  uri: ITextModel['uri'],
  location: CsxLocation
) {
  if (location.sourceCode == null || uri.toString() === currentModel.uri.toString()) {
    return;
  }

  const existing = monacoApi.editor.getModel(uri);
  const model = existing ?? monacoApi.editor.createModel(location.sourceCode, MONACO_LANGUAGE_ID, uri);
  if (model.getLanguageId() !== MONACO_LANGUAGE_ID) {
    monacoApi.editor.setModelLanguage(model, MONACO_LANGUAGE_ID);
  }

  if (existing && existing.getValue() !== location.sourceCode) {
    existing.setValue(location.sourceCode);
  }

  setModelNavigationEnabled(model, true);
}
