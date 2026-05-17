import { Link, useNavigate } from '@tanstack/react-router';
import { ArrowUpRight, PlayCircle, Plus, Search, StopCircle } from 'lucide-react';
import { useState } from 'react';
import { StatusBadge } from '@/components/StatusBadge';
import { TimeAgo } from '@/components/TimeAgo';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useUniEmuStore } from '@/store/uniemu-store';
import { formatNumber, formatUptime } from '@/utils/format';
import { localization } from '@/localization';
import { Emulator } from '@/types/uniemu';

const DefaultEmulator = {
  name: '',
  protocolId: 1,
  targetUrl: 'http://127.0.0.1:8080',
  intervalSec: 5,
} satisfies Partial<Emulator>;

export function EmulatorsListPage() {
  const emulators = useUniEmuStore((s) => s.emulators);
  const toggleStatus = useUniEmuStore((s) => s.toggleStatus);
  const createEmulator = useUniEmuStore((s) => s.createEmulator);
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<'all' | 'Running' | 'Stopped' | 'Error'>('all');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState(DefaultEmulator);

  const handleCreate = async () => {
    const name = form.name.trim();
    if (!name) return;
    const protocolId = Math.max(1, parseInt(form.protocolId, 10) || 1);
    const intervalSec = Math.max(1, parseInt(form.intervalSec, 10) || 5);
    const id = await createEmulator({
      name,
      protocolId,
      targetUrl: form.targetUrl.trim(),
      intervalSec,
    });
    setDialogOpen(false);
    setForm(DefaultEmulator);
    navigate({ to: '/emulators/$id', params: { id } });
  };

  const filtered = emulators.filter((e) => {
    if (filter !== 'all' && e.status !== filter) return false;
    return e.name.toLowerCase().includes(query.toLowerCase());
  });

  const filters = [
    { id: 'all', label: localization.routes.emulators.components.emulatorsListPage.allFilterLabel },
    { id: 'Running', label: localization.routes.emulators.components.emulatorsListPage.runningFilterLabel },
    { id: 'Stopped', label: localization.routes.emulators.components.emulatorsListPage.stoppedFilterLabel },
    { id: 'Error', label: localization.routes.emulators.components.emulatorsListPage.errorsFilterLabel },
  ] as const;

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            {localization.routes.emulators.components.emulatorsListPage.title}
          </h1>
          <p className="text-sm text-muted-foreground">
            {localization.routes.emulators.components.emulatorsListPage.description}
          </p>
        </div>
        <Button className="gap-2" onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" />
          {localization.routes.emulators.components.emulatorsListPage.createButtonLabel}
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 max-w-sm">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={localization.routes.emulators.components.emulatorsListPage.searchPlaceholder}
            className="pl-9"
          />
        </div>
        <div className="flex gap-1 rounded-md border border-border bg-card p-1">
          {filters.map((f) => (
            <button
              key={f.id}
              onClick={() => setFilter(f.id)}
              className={`rounded px-3 py-1 text-xs font-medium transition-colors ${filter === f.id
                ? 'bg-primary/15 text-primary'
                : 'text-muted-foreground hover:text-foreground'
                }`}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        {filtered.map((e) => (
          <div
            key={e.id}
            className="group rounded-lg border border-border bg-card p-4 transition-colors hover:border-primary/40"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <Link
                  to="/emulators/$id"
                  params={{ id: e.id }}
                  className="block font-mono text-base font-semibold text-foreground hover:text-primary"
                >
                  {e.name}
                </Link>
                <p className="mt-0.5 truncate font-mono text-[11px] text-muted-foreground">
                  {e.targetUrl}
                </p>
              </div>
              <StatusBadge status={e.status} />
            </div>

            {e.lastError && (
              <p className="mt-3 rounded border border-signal-offline/30 bg-signal-offline/5 px-2 py-1 text-[11px] text-signal-offline">
                {e.lastError}
              </p>
            )}

            <div className="mt-4 grid grid-cols-3 gap-2 border-t border-border/60 pt-3 text-center">
              <div>
                <p className="text-[10px] uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorsListPage.tagsCountLabel}
                </p>
                <p className="font-mono text-sm font-medium">{e.tagsCount}</p>
              </div>
              <div>
                <p className="text-[10px] uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorsListPage.uptimeLabel}
                </p>
                <p className="font-mono text-sm font-medium">{formatUptime(e.uptimeSec)}</p>
              </div>
              <div>
                <p className="text-[10px] uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorsListPage.requestsCountLabel}
                </p>
                <p className="font-mono text-sm font-medium">{formatNumber(e.totalRequests)}</p>
              </div>
            </div>

            <div className="mt-3 flex items-center justify-between text-[11px] text-muted-foreground">
              <span>
                {localization.routes.emulators.components.emulatorsListPage.lastRunPrefix}
                <TimeAgo iso={e.lastRun} />
              </span>
              <div className="flex gap-1">
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 px-2"
                  onClick={() => void toggleStatus(e.id)}
                >
                  {e.status === 'Running' ? (
                    <StopCircle className="h-3.5 w-3.5 text-signal-offline" />
                  ) : (
                    <PlayCircle className="h-3.5 w-3.5 text-signal-online" />
                  )}
                </Button>
                <Button asChild size="sm" variant="ghost" className="h-7 px-2">
                  <Link to="/emulators/$id" params={{ id: e.id }}>
                    <ArrowUpRight className="h-3.5 w-3.5" />
                  </Link>
                </Button>
              </div>
            </div>
          </div>
        ))}
      </div>

      {filtered.length === 0 && (
        <div className="rounded-lg border border-dashed border-border bg-card/40 p-12 text-center">
          <p className="text-sm text-muted-foreground">
            {localization.routes.emulators.components.emulatorsListPage.emptySearchMessage}
          </p>
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {localization.routes.emulators.components.emulatorsListPage.newEmulatorTitle}
            </DialogTitle>
            <DialogDescription>
              {localization.routes.emulators.components.emulatorsListPage.newEmulatorDescription}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="em-name">
                {localization.routes.emulators.components.emulatorsListPage.nameLabel}
              </Label>
              <Input
                id="em-name"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                autoFocus
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="em-protocol-id">
                {localization.routes.emulators.components.emulatorsListPage.protocolIdLabel}
              </Label>
              <Input
                id="em-protocol-id"
                type="number"
                min={1}
                value={form.protocolId}
                onChange={(e) => setForm((f) => ({ ...f, protocolId: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="em-url">
                {localization.routes.emulators.components.emulatorsListPage.targetUrlLabel}
              </Label>
              <Input
                id="em-url"
                type="url"
                value={form.targetUrl}
                onChange={(e) => setForm((f) => ({ ...f, targetUrl: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="em-interval">
                {localization.routes.emulators.components.emulatorsListPage.sendIntervalLabel}
              </Label>
              <Input
                id="em-interval"
                type="number"
                min={1}
                value={form.intervalSec}
                onChange={(e) => setForm((f) => ({ ...f, intervalSec: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {localization.routes.emulators.components.emulatorsListPage.cancelButtonLabel}
            </Button>
            <Button onClick={() => void handleCreate()} disabled={!form.name.trim()}>
              {localization.routes.emulators.components.emulatorsListPage.createButtonText}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
