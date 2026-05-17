export function DistributionBar({
  segments,
  total,
}: {
  segments: { label: string; value: number; className: string }[];
  total: number;
}) {
  const safeTotal = total || 1;
  return (
    <>
      <div className="mt-4 flex h-2.5 w-full overflow-hidden rounded-full bg-muted">
        {segments.map((s) =>
          s.value === 0 ? null : (
            <div
              key={s.label}
              className={s.className}
              style={{ width: `${(s.value / safeTotal) * 100}%` }}
              title={`${s.label}: ${s.value}`}
            />
          ),
        )}
      </div>
      <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-xs">
        {segments.map((s) => (
          <div key={s.label} className="flex items-center gap-2">
            <span className={`h-2 w-2 rounded-full ${s.className}`} />
            <span className="text-muted-foreground">{s.label}</span>
            <span className="font-mono font-medium tabular-nums text-foreground">
              {s.value}
            </span>
          </div>
        ))}
      </div>
    </>
  );
}
