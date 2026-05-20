import { localization } from '@/localization';
import type { SystemEvent } from '@/types/uniemu';

type EmulatorLogsTabProps = {
  events: SystemEvent[];
};

export function EmulatorLogsTab({ events }: EmulatorLogsTabProps) {
  return (
    <div className="rounded-lg border border-border bg-card">
      <div className="border-b border-border p-4">
        <h3 className="font-semibold">
          {localization.routes.emulators.components.emulatorDetailPage.eventsLogTitle}
        </h3>
        <p className="text-xs text-muted-foreground">
          {localization.routes.emulators.components.emulatorDetailPage.eventsCountLabel(events.length)}
        </p>
      </div>
      <div className="divide-y divide-border/40">
        {events.length === 0 && (
          <div className="p-8 text-center text-sm text-muted-foreground">
            {localization.routes.emulators.components.emulatorDetailPage.emptyEventsMessage}
          </div>
        )}
        {events.map((ev) => (
          <div key={ev.id} className="flex gap-3 px-4 py-2.5 font-mono text-xs">
            <span className="w-32 shrink-0 text-muted-foreground">
              {new Date(ev.timestamp).toLocaleString('ru-RU')}
            </span>
            <span className={`w-16 shrink-0 uppercase ${getEventLevelClassName(ev.level)}`}>
              {ev.level}
            </span>
            <span className="text-foreground">{ev.message}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function getEventLevelClassName(level: SystemEvent['level']): string {
  if (level === 'error') return 'text-signal-offline';
  if (level === 'warn') return 'text-signal-warning';
  if (level === 'success') return 'text-signal-online';
  return 'text-signal-info';
}
