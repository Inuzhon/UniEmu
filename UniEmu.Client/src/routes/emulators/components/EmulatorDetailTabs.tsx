import { localization } from '@/localization';

export const emulatorDetailTabs = [
  { id: 'overview', label: localization.routes.emulators.components.emulatorDetailPage.overviewTabLabel },
  { id: 'tags', label: localization.routes.emulators.components.emulatorDetailPage.tagsTabLabel },
  { id: 'monitoring', label: localization.routes.emulators.components.emulatorDetailPage.monitoringTabLabel },
  { id: 'logs', label: localization.routes.emulators.components.emulatorDetailPage.logsTabLabel },
] as const;

export type EmulatorDetailTabId = (typeof emulatorDetailTabs)[number]['id'];

type EmulatorDetailTabsProps = {
  tab: EmulatorDetailTabId;
  onTabChange: (tab: EmulatorDetailTabId) => void;
};

export function EmulatorDetailTabs({ tab, onTabChange }: EmulatorDetailTabsProps) {
  return (
    <div className="flex gap-1 border-b border-border">
      {emulatorDetailTabs.map((item) => (
        <button
          key={item.id}
          onClick={() => onTabChange(item.id)}
          className={`relative px-4 py-2 text-sm font-medium transition-colors ${
            tab === item.id ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          {item.label}
          {tab === item.id && (
            <span className="absolute -bottom-px left-0 h-0.5 w-full bg-primary" />
          )}
        </button>
      ))}
    </div>
  );
}
