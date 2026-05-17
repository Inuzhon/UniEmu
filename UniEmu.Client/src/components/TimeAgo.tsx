import { useEffect, useState } from "react";
import { timeAgo } from "@/utils/format";

/**
 * Renders a human-readable relative time string only on the client to
 * avoid SSR hydration mismatches (the server and client compute different
 * "X minutes ago" values because time advances between renders).
 */
export function TimeAgo({ iso, className }: { iso: string | null; className?: string }) {
  const [label, setLabel] = useState<string>("-");

  useEffect(() => {
    const update = () => setLabel(timeAgo(iso));
    update();
    const id = window.setInterval(update, 30_000);
    return () => window.clearInterval(id);
  }, [iso]);

  return <span className={className} suppressHydrationWarning>{label}</span>;
}
