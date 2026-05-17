import { TimeAgo } from '@/components/TimeAgo';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { CncProgram } from '@/types/uniemu';
import { FileText, Download, Save, X, Pencil } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { fmtSize } from '../utils/-index';
import { localization } from '@/localization';

export function CncViewer({
  file,
  emulatorName,
  onDownload,
  onSave,
}: {
  file: CncProgram;
  emulatorName?: string;
  onDownload: () => void;
  onSave: (patch: Partial<Pick<CncProgram, 'content' | 'description' | 'name'>>) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(file.content);
  const [description, setDescription] = useState(file.description);
  const dirty = editing && (draft !== file.content || description !== file.description);

  const handleSave = () => {
    onSave({ content: draft, description });
    setEditing(false);
  };

  const lineCount = file.isBinary ? 0 : file.content.split('\n').length;

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card/40 px-4 py-3">
        <div className="flex items-center gap-3">
          <FileText className="h-4 w-4 text-primary" />
          <span className="font-mono text-sm font-medium">{file.name}</span>
          <span
            className={`rounded px-1.5 py-px font-mono text-[10px] uppercase ${
              file.scope === 'shared' ? 'bg-accent/15 text-accent' : 'bg-primary/15 text-primary'
            }`}
          >
            {file.scope === 'shared' ? 'shared' : (emulatorName ?? 'emulator')}
          </span>
          {file.isBinary && (
            <span className="rounded bg-muted px-1.5 py-px font-mono text-[10px] uppercase text-muted-foreground">
              binary
            </span>
          )}
          {dirty && (
            <span className="rounded bg-signal-warning/15 px-1.5 py-px font-mono text-[10px] uppercase text-signal-warning">
              {localization.routes.cnc.components.cncViewer.text1}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 font-mono text-[11px] text-muted-foreground">
          <span>{fmtSize(file.sizeBytes)}</span>
          <span> </span>
          <span>
            {localization.routes.cnc.components.cncViewer.text2}
            <span> </span>
            <TimeAgo iso={file.updatedAt} />
          </span>
          <Button size="sm" variant="outline" className="gap-2" onClick={onDownload}>
            <Download className="h-3.5 w-3.5" />{' '}
            {localization.routes.cnc.components.cncViewer.text3}
          </Button>
          {!file.isBinary &&
            (editing ? (
              <>
                <Button
                  size="sm"
                  variant={dirty ? 'default' : 'outline'}
                  disabled={!dirty}
                  onClick={handleSave}
                  className="gap-2"
                >
                  <Save className="h-3.5 w-3.5" />{' '}
                  {localization.routes.cnc.components.cncViewer.text4}
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => {
                    setDraft(file.content);
                    setDescription(file.description);
                    setEditing(false);
                  }}
                  className="gap-2"
                >
                  <X className="h-3.5 w-3.5" />
                </Button>
              </>
            ) : (
              <Button
                size="sm"
                variant="outline"
                className="gap-2"
                onClick={() => setEditing(true)}
              >
                <Pencil className="h-3.5 w-3.5" />{' '}
                {localization.routes.cnc.components.cncViewer.text5}
              </Button>
            ))}
        </div>
      </div>

      {/* Description */}
      <div className="border-b border-border bg-card/20 px-4 py-2">
        <div className="text-[10px] uppercase tracking-wider text-muted-foreground">
          {localization.routes.cnc.components.cncViewer.text6}
        </div>
        {editing ? (
          <Input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder={localization.routes.cnc.components.cncViewer.text7}
            className="mt-1 h-8 text-sm"
          />
        ) : (
          <p className="mt-0.5 text-sm text-foreground/90">
            {file.description || (
              <span className="italic text-muted-foreground/60">
                {localization.routes.cnc.components.cncViewer.text8}
              </span>
            )}
          </p>
        )}
      </div>

      {/* Content */}
      <div className="editor-surface min-h-0 flex-1 overflow-auto">
        {file.isBinary ? (
          <div className="flex h-full items-center justify-center p-8">
            <div className="text-center text-muted-foreground">
              <FileText className="mx-auto h-10 w-10 opacity-40" />
              <p className="mt-3 text-sm">{localization.routes.cnc.components.cncViewer.text9}</p>
              <p className="mt-1 text-xs">{localization.routes.cnc.components.cncViewer.text10}</p>
            </div>
          </div>
        ) : editing ? (
          <Textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            spellCheck={false}
            className="editor-surface h-full min-h-full w-full resize-none rounded-none border-0 p-4 font-mono text-[13px] leading-6 focus-visible:ring-0"
          />
        ) : (
          <pre className="editor-text h-full w-full overflow-auto p-4 font-mono text-[13px] leading-6">
            <code>{file.content}</code>
          </pre>
        )}
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between border-t border-border bg-card/40 px-4 py-1.5 font-mono text-[11px] text-muted-foreground">
        <div className="flex items-center gap-3">
          <span className="text-signal-online">● gcode</span>
          <span>UTF-8</span>
          {!file.isBinary && (
            <span>
              {lineCount} {localization.routes.cnc.components.cncViewer.text11}
            </span>
          )}
        </div>
        <div>
          {localization.routes.cnc.components.cncViewer.text12}
          <TimeAgo iso={file.uploadedAt} />
        </div>
      </div>
    </div>
  );
}
