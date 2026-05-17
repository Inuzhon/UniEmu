import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { changelog, APP_VERSION } from '@/data/changelog';
import { cn } from '@/lib/utils';
import { localization } from '@/localization';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ChangelogDialog({ open, onOpenChange }: Props) {
  const [selected, setSelected] = useState<string>(APP_VERSION);
  const entry = changelog.find((c) => c.version === selected) ?? changelog[0];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl p-0 gap-0 overflow-hidden">
        <DialogHeader className="border-b border-border px-5 py-3 text-left">
          <DialogTitle className="font-mono text-sm tracking-widest text-muted-foreground">
            CHANGELOG
          </DialogTitle>
          <DialogDescription className="text-xs">
            {localization.components.changelogDialog.title}
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-[180px_1fr] h-[60vh]">
          <aside className="border-r border-border bg-sidebar/40 overflow-y-auto p-2">
            {changelog.map((c) => {
              const active = c.version === selected;
              const isCurrent = c.version === APP_VERSION;
              return (
                <button
                  key={c.version}
                  onClick={() => setSelected(c.version)}
                  className={cn(
                    'w-full text-left px-3 py-2 rounded-md mb-1 transition-colors',
                    active
                      ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                      : 'hover:bg-sidebar-accent/50 text-muted-foreground'
                  )}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-sm">v{c.version}</span>
                    {isCurrent && (
                      <span className="text-[9px] uppercase tracking-wider rounded bg-primary/15 text-primary px-1.5 py-0.5 ring-1 ring-primary/30">
                        now
                      </span>
                    )}
                  </div>
                  <div className="text-[10px] text-muted-foreground mt-0.5">{c.date}</div>
                </button>
              );
            })}
          </aside>

          <div className="overflow-y-auto px-6 py-5">
            <div className="mb-4 flex items-baseline gap-3">
              <h2 className="font-mono text-xl font-semibold text-foreground">v{entry.version}</h2>
              <span className="text-xs text-muted-foreground">{entry.date}</span>
            </div>
            <article className="prose-changelog">
              <ReactMarkdown>{entry.markdown}</ReactMarkdown>
            </article>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
