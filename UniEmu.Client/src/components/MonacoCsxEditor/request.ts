import { API_BASE_URL } from './constants';
import type { CsxTextRange, ITextModel, Position } from './types';

type RequestOptions = {
  range?: CsxTextRange;
  newName?: string;
  includeDeclaration?: boolean;
};

export async function request<T>(
  path: string,
  model: ITextModel,
  position?: Position,
  options: RequestOptions = {}
): Promise<T | null> {
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify({
        sourceCode: model.getValue(),
        documentUri: model.uri.toString(),
        position: position ? { line: position.lineNumber, column: position.column } : null,
        range: options.range,
        newName: options.newName,
        includeDeclaration: options.includeDeclaration ?? true,
      }),
    });

    if (!response.ok) return null;
    return (await response.json()) as T;
  } catch {
    return null;
  }
}
