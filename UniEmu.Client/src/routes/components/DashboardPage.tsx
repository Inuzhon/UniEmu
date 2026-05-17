import { Link } from '@tanstack/react-router';
import { Activity, AlertTriangle, ArrowUpRight, Cpu, Zap } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useUniEmuStore } from '@/store/uniemu-store';
import { TimeAgo } from '@/components/TimeAgo';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { SHOW_EVENTS_FEED } from '@/lib/feature-flags';
import { formatNumber } from '@/utils/format';
import type { Emulator } from '@/types/uniemu';
import { EmulatorTile } from './EmulatorTile';
import { DistributionBar } from './DistributionBar';
import { StatTile } from './StatTile';
import { localization } from '@/localization';

const hasRuntimeError = (e: Emulator) => e.status === 'Error' || !!e.lastError;

export function DashboardPage() {
  const emulators = useUniEmuStore((s) => s.emulators);
  const events = useUniEmuStore((s) => s.events);
  const toggleStatus = useUniEmuStore((s) => s.toggleStatus);
  const [query, setQuery] = useState('');

  const total = emulators.length;
  const running = emulators.filter((e) => e.status === 'Running' && !hasRuntimeError(e)).length;
  const stopped = emulators.filter((e) => e.status === 'Stopped' && !hasRuntimeError(e)).length;
  const idle = emulators.filter((e) => e.status === 'Idle' && !hasRuntimeError(e)).length;
  const errors = emulators.filter(hasRuntimeError).length;
  const totalRequests = emulators.reduce((s, e) => s + e.totalRequests, 0);
  const totalTags = emulators.reduce((s, e) => s + e.tagsCount, 0);
  const readiness = total ? Math.round((running / total) * 100) : 0;

  const filtered = useMemo(
    () =>
      emulators.filter((e) => e.name.toLowerCase().includes(query.toLowerCase())),
    [emulators, query]
  );

  const levelClass: Record<string, string> = {
    info: 'text-signal-info',
    success: 'text-signal-online',
    warn: 'text-signal-warning',
    error: 'text-signal-offline',
  };

  return (
    <div className="space-y-6 p-4 md:p-5">
      {/* Hero */}
      <section className="relative overflow-hidden rounded-2xl border border-border bg-linear-to-br from-card via-card to-primary/5 p-5 md:p-6">
        <div className="absolute -right-24 -top-24 h-64 w-64 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-3">
            <div className="inline-flex items-center gap-2 rounded-full border border-border bg-background/40 px-3 py-1 text-[11px] font-medium text-muted-foreground backdrop-blur">
              <span className="relative flex h-2 w-2">
                <span
                  className={`absolute inline-flex h-full w-full animate-ping rounded-full ${errors
                      ? 'bg-signal-offline/60'
                      : running
                        ? 'bg-signal-online/60'
                        : 'bg-muted-foreground/40'
                    }`}
                />
                <span
                  className={`relative inline-flex h-2 w-2 rounded-full ${errors
                      ? 'bg-signal-offline'
                      : running
                        ? 'bg-signal-online'
                        : 'bg-muted-foreground'
                    }`}
                />
              </span>
              {errors
                ? localization.routes.components.dashboardPage.text1(
                  errors,
                  errors === 1
                    ? localization.routes.components.dashboardPage.text29
                    : localization.routes.components.dashboardPage.text30
                )
                : running
                  ? localization.routes.components.dashboardPage.text2
                  : localization.routes.components.dashboardPage.text3}
            </div>
            <h1 className="text-3xl font-semibold tracking-tight md:text-4xl">
              {localization.routes.components.dashboardPage.text4}
            </h1>
            <p className="max-w-xl text-sm text-muted-foreground">
              {localization.routes.components.dashboardPage.text5}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline" size="sm" className="gap-2">
              <Link to="/emulators">
                {localization.routes.components.dashboardPage.text6}
                <ArrowUpRight className="h-3.5 w-3.5" />
              </Link>
            </Button>
            {/* <Button asChild size="sm" className="gap-2">
              <Link to="/emulators">
                <Plus className="h-3.5 w-3.5" />{' '}
                {localization.routes.components.dashboardPage.text7}
              </Link>
            </Button> */}
          </div>
        </div>

        {/* Readiness bar */}
        <div className="relative mt-6 grid grid-cols-1 gap-4 md:grid-cols-[1fr_auto] md:items-end">
          <div>
            <div className="flex items-baseline justify-between text-xs text-muted-foreground">
              <span className="uppercase tracking-wider">
                {localization.routes.components.dashboardPage.text8}
              </span>
              <span className="font-mono">
                {running} / {total} {localization.routes.components.dashboardPage.text9}
              </span>
            </div>
            <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-linear-to-r from-signal-online to-primary transition-all duration-500"
                style={{ width: `${readiness}%` }}
              />
            </div>
          </div>
          <div className="font-mono text-4xl font-semibold tabular-nums text-foreground md:text-5xl">
            {readiness}
            <span className="ml-1 text-lg text-muted-foreground">%</span>
          </div>
        </div>
      </section>

      {/* Stat strip */}
      <section className="grid grid-cols-2 gap-2 lg:grid-cols-4">
        <StatTile
          icon={Cpu}
          label={localization.routes.components.dashboardPage.text10}
          value={String(total)}
          hint={localization.routes.components.dashboardPage.text11}
        />
        <StatTile
          icon={Activity}
          label={localization.routes.components.dashboardPage.text12}
          value={String(running)}
          hint={localization.routes.components.dashboardPage.text13(stopped, idle)}
          accent="online"
        />
        <StatTile
          icon={AlertTriangle}
          label={localization.routes.components.dashboardPage.text14}
          value={String(errors)}
          hint={
            errors
              ? localization.routes.components.dashboardPage.text15
              : localization.routes.components.dashboardPage.text16
          }
          accent={errors ? 'offline' : 'muted'}
        />
        <StatTile
          icon={Zap}
          label={localization.routes.components.dashboardPage.text17}
          value={formatNumber(totalRequests)}
          hint={localization.routes.components.dashboardPage.text18(totalTags)}
          accent="info"
        />
      </section>
{/*
      <section className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
              {localization.routes.components.dashboardPage.text19}
            </h2>
          </div>
          <span className="font-mono text-xs text-muted-foreground">
            {total} {localization.routes.components.dashboardPage.text20}
          </span>
        </div>
        <DistributionBar
          segments={[
            { label: localization.routes.components.dashboardPage.running, value: running, className: 'bg-signal-online' },
            // { label: localization.routes.components.dashboardPage.idle, value: idle, className: 'bg-signal-info' },
            { label: localization.routes.components.dashboardPage.stopped, value: stopped, className: 'bg-muted-foreground/60' },
            { label: localization.routes.components.dashboardPage.errors, value: errors, className: 'bg-signal-offline' },
          ]}
          total={total}
        />
      </section> */}

      {/* Emulators + (optional) events */}
      <section className={`grid grid-cols-1 gap-4 ${SHOW_EVENTS_FEED ? 'xl:grid-cols-3' : ''}`}>
        <div
          className={`rounded-xl border border-border bg-card ${SHOW_EVENTS_FEED ? 'xl:col-span-2' : ''}`}
        >
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4">
            <div>
              <h2 className="font-semibold">
                {localization.routes.components.dashboardPage.text21}
              </h2>
              <p className="text-xs text-muted-foreground">
                {localization.routes.components.dashboardPage.text22}
              </p>
            </div>
            <Input
              placeholder={localization.routes.components.dashboardPage.text23}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              className="w-full sm:w-64"
            />
          </div>

          {filtered.length === 0 ? (
            <div className="p-12 text-center text-sm text-muted-foreground">
              {total === 0
                ? localization.routes.components.dashboardPage.text24
                : localization.routes.components.dashboardPage.text25}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3 p-4 md:grid-cols-2">
              {filtered.map((e) => (
                <EmulatorTile
                  key={e.id}
                  id={e.id}
                  name={e.name}
                  status={hasRuntimeError(e) ? 'Error' : e.status}
                  targetUrl={e.targetUrl}
                  lastError={e.lastError}
                  lastRun={e.lastRun}
                  uptimeSec={e.uptimeSec}
                  totalRequests={e.totalRequests}
                  onToggle={() => void toggleStatus(e.id)}
                />
              ))}
            </div>
          )}
        </div>

        {SHOW_EVENTS_FEED && (
          <div className="rounded-xl border border-border bg-card">
            <div className="border-b border-border p-4">
              <h2 className="font-semibold">
                {localization.routes.components.dashboardPage.text26}
              </h2>
              <p className="text-xs text-muted-foreground">
                {localization.routes.components.dashboardPage.text27}
                {events.length} {localization.routes.components.dashboardPage.text28}
              </p>
            </div>
            <div className="max-h-[520px] overflow-y-auto">
              {events.map((ev) => (
                <div
                  key={ev.id}
                  className="flex gap-3 border-b border-border/40 px-4 py-2.5 text-sm last:border-0"
                >
                  <span
                    className={`mt-1.5 signal-dot shrink-0 ${ev.level === 'success'
                        ? 'bg-signal-online'
                        : ev.level === 'warn'
                          ? 'bg-signal-warning'
                          : ev.level === 'error'
                            ? 'bg-signal-offline'
                            : 'bg-signal-info'
                      }`}
                  />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-xs text-foreground">{ev.message}</p>
                    <div className="mt-0.5 flex items-center gap-2 text-[11px] text-muted-foreground">
                      <span className={`font-mono ${levelClass[ev.level]}`}>{ev.emulatorName}</span>
                      <span>•</span>
                      <span>
                        <TimeAgo iso={ev.timestamp} />
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
