import { Settings as SettingsIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { TimeAgo } from '@/components/TimeAgo';
import { localization } from '@/localization';
import type { Emulator } from '@/types/uniemu';
import { formatNumber, formatUptime } from '@/utils/format';

type EmulatorOverviewTabProps = {
  emulator: Emulator;
  onEditConfig: () => void;
};

export function EmulatorOverviewTab({ emulator, onEditConfig }: EmulatorOverviewTabProps) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
      <div className="rounded-lg border border-border bg-card p-4 lg:col-span-2">
        <h3 className="mb-4 text-sm font-semibold uppercase tracking-wider text-muted-foreground">
          {localization.routes.emulators.components.emulatorDetailPage.configurationTitle}
        </h3>
        <dl className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.targetUrlLabel}
            </dt>
            <dd className="mt-1 font-mono text-xs">{emulator.targetUrl}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.intervalLabel}
            </dt>
            <dd className="mt-1 font-mono">
              {emulator.intervalSec}{' '}
              {localization.routes.emulators.components.emulatorDetailPage.secondsUnitLabel}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.emulatorIdLabel}
            </dt>
            <dd className="mt-1 font-mono text-xs">{emulator.id}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.protocolIdLabel}
            </dt>
            <dd className="mt-1 font-mono">{emulator.protocolId}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.payloadTagsLabel}
            </dt>
            <dd className="mt-1 font-mono">{emulator.tagsCount}</dd>
          </div>
        </dl>
        <Button
          variant="outline"
          size="sm"
          className="mt-6 gap-2"
          onClick={onEditConfig}
        >
          <SettingsIcon className="h-3.5 w-3.5" />{' '}
          {localization.routes.emulators.components.emulatorDetailPage.editConfigurationButtonLabel}
        </Button>
      </div>

      <div className="space-y-4">
        <div className="rounded-lg border border-border bg-card p-4">
          <p className="text-xs uppercase tracking-wider text-muted-foreground">
            {localization.routes.emulators.components.emulatorDetailPage.uptimeLabel}
          </p>
          <p className="mt-2 font-mono text-2xl font-semibold">
            {formatUptime(emulator.uptimeSec)}
          </p>
        </div>
        <div className="rounded-lg border border-border bg-card p-4">
          <p className="text-xs uppercase tracking-wider text-muted-foreground">
            {localization.routes.emulators.components.emulatorDetailPage.totalRequestsLabel}
          </p>
          <p className="mt-2 font-mono text-2xl font-semibold">
            {formatNumber(emulator.totalRequests)}
          </p>
        </div>
        <div className="rounded-lg border border-border bg-card p-4">
          <p className="text-xs uppercase tracking-wider text-muted-foreground">
            {localization.routes.emulators.components.emulatorDetailPage.lastRunLabel}
          </p>
          <p className="mt-2 font-mono text-sm">
            <TimeAgo iso={emulator.lastRun} />
          </p>
        </div>
      </div>
    </div>
  );
}
