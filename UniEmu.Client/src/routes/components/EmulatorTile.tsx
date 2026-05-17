import { StatusBadge } from "@/components/StatusBadge";
import { TimeAgo } from "@/components/TimeAgo";
import { formatUptime, formatNumber } from "@/utils/format";
import { Gauge, Radio, CircleDot, StopCircle, PlayCircle, ArrowUpRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Link } from "@tanstack/react-router";

export function EmulatorTile({
  id,
  name,
  status,
  targetUrl,
  lastError,
  lastRun,
  uptimeSec,
  totalRequests,
  onToggle,
}: {
  id: string;
  name: string;
  status: "Running" | "Stopped" | "Error" | "Idle";
  targetUrl: string;
  lastError: string | null;
  lastRun: string | null;
  uptimeSec: number;
  totalRequests: number;
  onToggle: () => void;
}) {
  const statusGlow: Record<string, string> = {
    Running: "before:bg-signal-online",
    Error: "before:bg-signal-offline",
    Idle: "before:bg-signal-info",
    Stopped: "before:bg-muted-foreground/40",
  };
  return (
    <div
      className={`group relative overflow-hidden rounded-lg border border-border bg-background/40 p-4 transition-colors hover:border-primary/40 before:absolute before:left-0 before:top-0 before:h-full before:w-0.5 ${statusGlow[status]}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <Link
            to="/emulators/$id"
            params={{ id }}
            className="block truncate font-mono text-sm font-semibold text-foreground hover:text-primary"
          >
            {name}
          </Link>
          <p className="mt-0.5 truncate font-mono text-[11px] text-muted-foreground">
            {targetUrl}
          </p>
        </div>
        <StatusBadge status={status} />
      </div>

      {lastError && (
        <p className="mt-2 truncate rounded border border-signal-offline/30 bg-signal-offline/5 px-2 py-1 text-[11px] text-signal-offline">
          {lastError}
        </p>
      )}

      <div className="mt-3 grid grid-cols-3 gap-2 border-t border-border/60 pt-2 text-[11px]">
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <Gauge className="h-3 w-3" />
          <span className="font-mono text-foreground">{formatUptime(uptimeSec)}</span>
        </div>
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <Radio className="h-3 w-3" />
          <span className="font-mono text-foreground">{formatNumber(totalRequests)}</span>
        </div>
        <div className="flex items-center justify-end gap-1.5 text-muted-foreground">
          <CircleDot className="h-3 w-3" />
          <TimeAgo iso={lastRun} />
        </div>
      </div>

      <div className="mt-3 flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100">
        <Button size="sm" variant="ghost" className="h-7 px-2" onClick={onToggle}>
          {status === "Running" ? (
            <StopCircle className="h-3.5 w-3.5 text-signal-offline" />
          ) : (
            <PlayCircle className="h-3.5 w-3.5 text-signal-online" />
          )}
        </Button>
        <Button asChild size="sm" variant="ghost" className="h-7 px-2">
          <Link to="/emulators/$id" params={{ id }}>
            <ArrowUpRight className="h-3.5 w-3.5" />
          </Link>
        </Button>
      </div>
    </div>
  );
}
