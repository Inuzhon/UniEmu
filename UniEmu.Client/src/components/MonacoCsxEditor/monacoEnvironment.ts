import { loader } from '@monaco-editor/react';
import * as monaco from 'monaco-editor';
import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';

if (
  typeof window !== 'undefined' &&
  !(window as unknown as { MonacoEnvironment?: unknown }).MonacoEnvironment
) {
  (window as unknown as { MonacoEnvironment: { getWorker: () => Worker } }).MonacoEnvironment = {
    getWorker: () => new editorWorker(),
  };
}

loader.config({ monaco });

export { monaco };
