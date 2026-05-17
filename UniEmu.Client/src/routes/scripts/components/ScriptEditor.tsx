import { buildCsxDocumentUri } from '@/components/csx-language-client';
import { MonacoCsxEditor } from '@/components/MonacoCsxEditor';
import { TimeAgo } from '@/components/TimeAgo';
import { ScriptFile } from '@/types/uniemu';
import { FileCode2, Pencil, Save, Share2, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { localization } from '@/localization';

const UNSAVED_CHANGES_MESSAGE = 'Unsaved script changes will be lost. Continue?';

export function ScriptEditor({
  file,
  emulatorName,
  onSave,
  onDirtyChange,
}: {
  file: ScriptFile;
  emulatorName?: string;
  onSave: (content: string) => Promise<void>;
  onDirtyChange: (dirty: boolean) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(file.content);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const dirty = editing && draft !== file.content;

  useEffect(() => {
    onDirtyChange(dirty);
  }, [dirty, onDirtyChange]);

  const handleSave = async () => {
    if (!dirty || saving) return;
    setSaving(true);
    setSaveError(null);

    try {
      await onSave(draft);
      setEditing(false);
    } catch (error) {
      setSaveError(readApiError(error));
    } finally {
      setSaving(false);
    }
  };

  const cancelEditing = () => {
    if (dirty && !confirm(UNSAVED_CHANGES_MESSAGE)) return;
    setDraft(file.content);
    setSaveError(null);
    setEditing(false);
  };

  const startEditing = () => {
    setDraft(file.content);
    setSaveError(null);
    setEditing(true);
  };

  const visibleContentLength = editing ? draft.length : file.content.length;

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Editor header */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card/40 px-4 py-3">
        <div className="flex items-center gap-3">
          <FileCode2 className="h-4 w-4 text-primary" />
          <span className="font-mono text-sm font-medium">{file.name}</span>
          <span
            className={`rounded px-1.5 py-px font-mono text-[10px] uppercase ${
              file.scope === 'shared' ? 'bg-accent/15 text-accent' : 'bg-primary/15 text-primary'
            }`}
          >
            {file.scope === 'shared' ? 'shared' : (emulatorName ?? 'emulator')}
          </span>
          {dirty && (
            <span className="rounded bg-signal-warning/15 px-1.5 py-px font-mono text-[10px] uppercase text-signal-warning">
              {localization.routes.scripts.components.scriptEditor.dirtyBadgeLabel}
            </span>
          )}
        </div>
        <div className="flex items-center gap-3 font-mono text-[11px] text-muted-foreground">
          <span>
            {(visibleContentLength / 1024).toFixed(2)}{' '}
            {localization.routes.scripts.components.scriptEditor.kilobytesUnitLabel}
          </span>
          <span> </span>
          <span>
            {localization.routes.scripts.components.scriptEditor.updatedBadgeLabel}
            <span> </span>
            <TimeAgo iso={file.updatedAt} />
          </span>
          {editing ? (
            <>
              <Button
                size="sm"
                variant={dirty ? 'default' : 'outline'}
                disabled={!dirty || saving}
                onClick={() => void handleSave()}
                className="gap-2"
              >
                <Save className="h-3.5 w-3.5" />{' '}
                {localization.routes.scripts.components.scriptEditor.saveButtonLabel}
              </Button>
              <Button size="sm" variant="ghost" onClick={cancelEditing} className="gap-2">
                <X className="h-3.5 w-3.5" />
              </Button>
            </>
          ) : (
            <Button size="sm" variant="outline" className="gap-2" onClick={startEditing}>
              <Pencil className="h-3.5 w-3.5" />{' '}
              {localization.routes.scripts.components.scriptEditor.editButtonLabel}
            </Button>
          )}
        </div>
      </div>

      {saveError && (
        <div className="border-b border-destructive/40 bg-destructive/10 px-4 py-2 font-mono text-xs text-destructive">
          {saveError}
        </div>
      )}

      <div className="editor-surface min-h-0 flex-1">
        <MonacoCsxEditor
          value={editing ? draft : file.content}
          onChange={(value) => {
            if (editing) setDraft(value);
          }}
          documentUri={buildCsxDocumentUri({
            id: file.id,
            name: file.name,
            scope: file.scope,
            emulatorId: file.emulatorId,
          })}
          readOnly={!editing}
        />
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between border-t border-border bg-card/40 px-4 py-1.5 font-mono text-[11px] text-muted-foreground">
        <div className="flex items-center gap-3">
          <span className="text-signal-online">● csx</span>
          <span>UTF-8</span>
          <span>LF</span>
        </div>
        <div className="flex items-center gap-3">
          {file.scope === 'shared' && (
            <span className="flex items-center gap-1">
              <Share2 className="h-3 w-3" />{' '}
              {localization.routes.scripts.components.scriptEditor.sharedScopeHint}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

function readApiError(error: unknown) {
  if (error instanceof Error && 'body' in error) {
    const body = String((error as { body?: unknown }).body ?? '');
    try {
      const parsed = JSON.parse(body) as {
        message?: string;
        diagnostics?: Array<{ code?: string; message?: string }>;
      };
      const firstDiagnostic = parsed.diagnostics?.[0];
      if (firstDiagnostic) {
        return `${firstDiagnostic.code ?? 'CSX'}: ${firstDiagnostic.message ?? parsed.message ?? error.message}`;
      }

      return parsed.message ?? error.message;
    } catch {
      return body || error.message;
    }
  }

  return error instanceof Error
    ? error.message
    : localization.routes.scripts.components.scriptEditor.saveErrorMessage;
}
