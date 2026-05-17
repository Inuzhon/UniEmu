import { localization } from "@/localization";
import { useMatches } from "@tanstack/react-router";
import { ChevronLeft, Link } from "lucide-react";

export function Breadcrumbs() {
  const matches = useMatches();
  const crumbs = matches
    .filter((m) => m.pathname !== '/' || matches.length === 1)
    .map((m) => {
      const seg =
        m.pathname === '/'
          ? localization.components.layout.appLayout.dashboardBreadcrumbLabel
          : m.pathname.split('/').filter(Boolean).pop();
      const labels: Record<string, string> = {
        emulators: localization.components.layout.appLayout.emulatorsBreadcrumbLabel,
        cnc: localization.components.layout.appLayout.cncStorageBreadcrumbLabel,
        scripts: localization.components.layout.appLayout.scriptsBreadcrumbLabel,
        logs: localization.components.layout.appLayout.logsBreadcrumbLabel,
        settings: localization.components.layout.appLayout.settingsBreadcrumbLabel,
      };
      const label = seg ? (labels[seg] ?? decodeURIComponent(seg)) : '-';
      return { pathname: m.pathname, label };
    });

  return (
    <nav className="flex items-center gap-1 text-sm">
      {crumbs.map((c, i) => (
        <div key={c.pathname} className="flex items-center gap-1">
          {i > 0 && <ChevronLeft className="h-3.5 w-3.5 rotate-180 text-muted-foreground" />}
          <Link
            to={c.pathname}
            className={
              i === crumbs.length - 1
                ? 'font-medium text-foreground'
                : 'text-muted-foreground hover:text-foreground'
            }
          >
            {c.label}
          </Link>
        </div>
      ))}
    </nav>
  );
}
