import { Input } from '@/components/ui/input';
import { Globe2, FolderOpen } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ScriptScope } from '@/types/uniemu';
import { localization } from '@/localization';

export interface NewScriptDraft {
  name: string;
  scope: ScriptScope;
  emulatorId?: string;
}

export function CreateScriptModal({
  draft,
  emulators,
  onChange,
  onCancel,
  onSubmit,
}: {
  draft: NewScriptDraft;
  emulators: { id: string; name: string }[];
  onChange: (d: NewScriptDraft) => void;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-2xl">
        <h2 className="text-lg font-semibold">
          {localization.routes.scripts.components.createScriptModal.text1}
        </h2>
        <p className="mt-1 text-xs text-muted-foreground">
          {localization.routes.scripts.components.createScriptModal.text2}
          <span className="mx-1 font-mono text-primary">#load</span>.
        </p>

        <div className="mt-5 space-y-4">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wider text-muted-foreground">
              {localization.routes.scripts.components.createScriptModal.text3}
            </label>
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => onChange({ ...draft, scope: 'shared', emulatorId: undefined })}
                className={`flex items-center gap-2 rounded-md border p-2.5 text-left text-sm transition-colors ${
                  draft.scope === 'shared'
                    ? 'border-accent bg-accent/10 text-accent'
                    : 'border-border text-muted-foreground hover:border-border/80'
                }`}
              >
                <Globe2 className="h-4 w-4" />{' '}
                {localization.routes.scripts.components.createScriptModal.text4}
              </button>
              <button
                onClick={() =>
                  onChange({
                    ...draft,
                    scope: 'emulator',
                    emulatorId: draft.emulatorId ?? emulators[0]?.id,
                  })
                }
                className={`flex items-center gap-2 rounded-md border p-2.5 text-left text-sm transition-colors ${
                  draft.scope === 'emulator'
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-border text-muted-foreground hover:border-border/80'
                }`}
              >
                <FolderOpen className="h-4 w-4" />{' '}
                {localization.routes.scripts.components.createScriptModal.text5}
              </button>
            </div>
          </div>

          {draft.scope === 'emulator' && (
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wider text-muted-foreground">
                {localization.routes.scripts.components.createScriptModal.text6}
              </label>
              <select
                value={draft.emulatorId ?? ''}
                onChange={(e) => onChange({ ...draft, emulatorId: e.target.value })}
                className="w-full rounded-md border border-input bg-background px-3 py-2 font-mono text-sm outline-none focus:border-primary"
              >
                {emulators.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>
          )}

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wider text-muted-foreground">
              {localization.routes.scripts.components.createScriptModal.text7}
            </label>
            <Input
              autoFocus
              value={draft.name}
              spellCheck={false}
              onChange={(e) => onChange({ ...draft, name: e.target.value })}
              placeholder={localization.routes.scripts.components.createScriptModal.text8}
              className="font-mono"
              onKeyDown={(e) => {
                if (e.key === 'Enter') onSubmit();
                if (e.key === 'Escape') onCancel();
              }}
            />
            <p className="mt-1 text-[11px] text-muted-foreground">
              {localization.routes.scripts.components.createScriptModal.text9}
              <span className="font-mono">.csx</span>{' '}
              {localization.routes.scripts.components.createScriptModal.text10}
            </p>
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <Button variant="outline" size="sm" onClick={onCancel}>
            {localization.routes.scripts.components.createScriptModal.text11}
          </Button>
          <Button size="sm" onClick={onSubmit} disabled={!draft.name.trim()}>
            {localization.routes.scripts.components.createScriptModal.text12}
          </Button>
        </div>
      </div>
    </div>
  );
}
