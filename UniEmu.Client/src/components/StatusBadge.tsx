import type { EmulatorStatus } from '@/types/uniemu';
import { localization } from '@/localization';

const config: Record<EmulatorStatus, { label: string; dot: string; text: string; bg: string }> = {
  Running: {
    label: localization.components.statusBadge.text1,
    dot: 'bg-signal-online pulse-online',
    text: 'text-signal-online',
    bg: 'bg-signal-online/10 border-signal-online/30',
  },
  Stopped: {
    label: localization.components.statusBadge.text2,
    dot: 'bg-muted-foreground',
    text: 'text-muted-foreground',
    bg: 'bg-muted/40 border-border',
  },
  Error: {
    label: localization.components.statusBadge.text3,
    dot: 'bg-signal-offline',
    text: 'text-signal-offline',
    bg: 'bg-signal-offline/10 border-signal-offline/30',
  },
  // Idle: {
  //   label: localization.components.statusBadge.text4,
  //   dot: 'bg-signal-warning',
  //   text: 'text-signal-warning',
  //   bg: 'bg-signal-warning/10 border-signal-warning/30',
  // },
};

export function StatusBadge({ status }: { status: EmulatorStatus }) {
  const normalized = (
    typeof status === 'string'
      ? ((status.charAt(0).toUpperCase() + status.slice(1).toLowerCase()) as EmulatorStatus)
      : status
  ) as EmulatorStatus;
  const c = config[normalized] ?? config.Stopped;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded border px-2 py-0.5 font-mono text-[10px] uppercase tracking-wider ${c.bg} ${c.text}`}
    >
      <span className={`signal-dot ${c.dot}`} />
      {c.label}
    </span>
  );
}
