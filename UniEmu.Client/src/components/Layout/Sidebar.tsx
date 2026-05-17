import { cn } from '@/lib/utils';
import { localization } from '@/localization';
import { Link, useRouterState } from '@tanstack/react-router';
import {
  Sun,
  Moon,
  PanelLeftOpen,
  PanelLeftClose,
  FolderCog,
  LayoutDashboard,
  Cpu,
  FileCode2,
} from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '../ui/tooltip';

export type NavItem = {
  to: string;
  label: string;
  icon: typeof LayoutDashboard;
  exact?: boolean;
};

export const navItems: NavItem[] = [
  {
    to: '/',
    label: localization.components.layout.appLayout.dashboardNavLabel,
    icon: LayoutDashboard,
    exact: true,
  },
  { to: '/emulators', label: localization.components.layout.appLayout.emulatorsNavLabel, icon: Cpu },
  { to: '/cnc', label: localization.components.layout.appLayout.cncStorageNavLabel, icon: FolderCog },
  { to: '/scripts', label: localization.components.layout.appLayout.scriptsNavLabel, icon: FileCode2 },
  // TODO: Временно скрыты, не удалять
  // { to: '/logs', label: localization.components.layout.appLayout.logsNavLabel, icon: ScrollText },
  // { to: '/settings', label: localization.components.layout.appLayout.settingsNavLabel, icon: Settings },
];

export function Sidebar({
  collapsed,
  onToggle,
  onOpenChangelog: _onOpenChangelog,
  theme,
  onThemeToggle,
}: {
  collapsed: boolean;
  onToggle: () => void;
  onOpenChangelog: () => void;
  theme: 'light' | 'dark';
  onThemeToggle: () => void;
}) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });

  return (
    <TooltipProvider delayDuration={100}>
      <aside
        className={cn(
          'flex h-screen shrink-0 flex-col border-r border-sidebar-border bg-sidebar transition-[width] duration-200',
          collapsed ? 'w-14' : 'w-60'
        )}
      >
        <div
          className={cn(
            'flex h-14 items-center gap-2 border-b border-sidebar-border',
            collapsed ? 'justify-center px-2' : 'px-4'
          )}
        >
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-gradient-to-br from-primary to-primary/60 ring-1 ring-primary/40 shadow-[0_0_12px_-2px_hsl(var(--primary)/0.5)]">
            <svg
              viewBox="0 0 24 24"
              className="h-4 w-4 text-primary-foreground"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M4 5v9a6 6 0 0 0 12 0V5" />
              <path d="M16 5h4v9a10 10 0 0 1-20 0" opacity="0.4" />
            </svg>
          </div>
          {!collapsed && (
            <div className="flex flex-col leading-tight">
              <span className="font-mono text-sm font-semibold tracking-wider text-sidebar-foreground">
                UNIEMU
              </span>
              <span className="text-[10px] uppercase tracking-widest text-muted-foreground">
                Web Console
              </span>
            </div>
          )}
        </div>

        <nav className={cn('flex-1 overflow-y-auto', collapsed ? 'p-1.5' : 'p-2')}>
          {navItems.map((item) => {
            const active = item.exact
              ? pathname === item.to
              : pathname === item.to || pathname.startsWith(item.to + '/');
            const Icon = item.icon;

            const link = (
              <Link
                to={item.to}
                className={cn(
                  'group relative flex items-center rounded-md text-sm transition-colors',
                  collapsed ? 'h-10 w-full justify-center' : 'gap-3 px-3 py-2',
                  active
                    ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                    : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground'
                )}
              >
                {active && (
                  <span className="absolute left-0 top-1/2 h-5 -translate-y-1/2 w-0.5 rounded-r bg-primary" />
                )}
                <Icon className="h-4 w-4 shrink-0" />
                {!collapsed && <span>{item.label}</span>}
              </Link>
            );

            if (collapsed) {
              return (
                <Tooltip key={item.to}>
                  <TooltipTrigger asChild>{link}</TooltipTrigger>
                  <TooltipContent
                    side="right"
                    className="bg-popover text-popover-foreground border border-border"
                  >
                    {item.label}
                  </TooltipContent>
                </Tooltip>
              );
            }
            return <div key={item.to}>{link}</div>;
          })}
        </nav>

        <div className={cn('border-t border-sidebar-border', collapsed ? 'p-1.5' : 'p-3')}>
          {/* Theme toggle */}
          {collapsed ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  onClick={onThemeToggle}
                  className="mb-1 flex h-9 w-full items-center justify-center rounded-md text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground transition-colors"
                  aria-label={
                    theme === 'dark'
                      ? localization.components.layout.appLayout.switchToLightThemeAriaLabel
                      : localization.components.layout.appLayout.switchToDarkThemeAriaLabel
                  }
                >
                  {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                </button>
              </TooltipTrigger>
              <TooltipContent side="right">
                {theme === 'dark'
                  ? localization.components.layout.appLayout.collapsedLightThemeTooltip
                  : localization.components.layout.appLayout.collapsedDarkThemeTooltip}
              </TooltipContent>
            </Tooltip>
          ) : (
            <button
              onClick={onThemeToggle}
              className="mb-2 flex w-full items-center justify-between gap-2 rounded-md px-3 py-2 text-xs text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground transition-colors"
            >
              <span>
                {theme === 'dark'
                  ? localization.components.layout.appLayout.expandedLightThemeLabel
                  : localization.components.layout.appLayout.expandedDarkThemeLabel}
              </span>
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </button>
          )}

          {/* Collapse toggle */}
          {collapsed ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  onClick={onToggle}
                  className="flex h-9 w-full items-center justify-center rounded-md text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground transition-colors"
                  aria-label={localization.components.layout.appLayout.expandMenuAriaLabel}
                >
                  <PanelLeftOpen className="h-4 w-4" />
                </button>
              </TooltipTrigger>
              <TooltipContent side="right">
                {localization.components.layout.appLayout.expandMenuTooltip}
              </TooltipContent>
            </Tooltip>
          ) : (
            <button
              onClick={onToggle}
              className="flex w-full items-center justify-between gap-2 rounded-md px-3 py-2 text-xs text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground transition-colors"
            >
              <span>{localization.components.layout.appLayout.collapseMenuLabel}</span>
              <PanelLeftClose className="h-3.5 w-3.5" />
            </button>
          )}

          {/* Version button */}
          {/* {collapsed ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  onClick={onOpenChangelog}
                  className="mt-1 flex h-9 w-full items-center justify-center rounded-md bg-sidebar-accent/40 font-mono text-[10px] text-sidebar-foreground hover:bg-sidebar-accent transition-colors"
                  aria-label={localization.components.layout.appLayout.version(APP_VERSION)}
                >
                  v{APP_VERSION.split('.').slice(0, 2).join('.')}
                </button>
              </TooltipTrigger>
              <TooltipContent side="right">
                {localization.components.layout.appLayout.version(APP_VERSION)}
                {localization.components.layout.appLayout.openChangelogSuffix}
              </TooltipContent>
            </Tooltip>
          ) : (
            <button
              onClick={onOpenChangelog}
              className="flex w-full items-center justify-between gap-2 rounded-md bg-sidebar-accent/40 px-3 py-2 hover:bg-sidebar-accent transition-colors"
            >
              <div className="flex flex-col items-start leading-tight">
                <span className="text-[10px] uppercase tracking-widest text-muted-foreground">
                  {localization.components.layout.appLayout.versionLabel}
                </span>
                <span className="font-mono text-xs text-sidebar-foreground">v{APP_VERSION}</span>
              </div>
              <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
            </button>
          )} */}
        </div>
      </aside>
    </TooltipProvider>
  );
}
