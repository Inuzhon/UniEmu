import { Activity } from "lucide-react";

export function StatTile({
  icon: Icon,
  label,
  value,
  hint,
  accent = "default",
}: {
  icon: typeof Activity;
  label: string;
  value: string;
  hint?: string;
  accent?: "default" | "online" | "offline" | "info" | "muted";
}) {
  const accents: Record<string, string> = {
    default: "text-foreground",
    online: "text-signal-online",
    offline: "text-signal-offline",
    info: "text-signal-info",
    muted: "text-muted-foreground",
  };
  return (
    <div className="group rounded-xl border border-border bg-card p-4 transition-colors hover:border-primary/40">
      <div className="flex items-center justify-between text-muted-foreground">
        <span className="text-[11px] uppercase tracking-wider">{label}</span>
        <Icon className={`h-3.5 w-3.5 ${accents[accent]}`} />
      </div>
      <p className={`mt-2 font-mono text-3xl font-semibold tabular-nums ${accents[accent]}`}>
        {value}
      </p>
      {hint && <p className="mt-1 text-[11px] text-muted-foreground">{hint}</p>}
    </div>
  );
}
