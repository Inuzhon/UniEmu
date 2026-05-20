import { useUniEmuStore } from '@/store/uniemu-store';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Slider } from '@/components/ui/slider';
import { TELEMETRY_PACKET_RETENTION_LIMIT } from '@/lib/constants';
import { localization } from '@/localization';

export function SettingsPage() {
  const retention = useUniEmuStore((s) => s.packetRetention);
  const setRetention = useUniEmuStore((s) => s.setPacketRetention);

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold">
          {localization.routes.settings.components.settingsPage.title}
        </h1>
        <p className="text-sm text-muted-foreground">
          {localization.routes.settings.components.settingsPage.description}
        </p>
      </div>

      <div className="rounded-lg border border-border bg-card p-6">
        <h2 className="mb-1 text-sm font-semibold uppercase tracking-wider text-muted-foreground">
          {localization.routes.settings.components.settingsPage.telemetrySectionTitle}
        </h2>
        <p className="mb-6 text-xs text-muted-foreground">
          {localization.routes.settings.components.settingsPage.telemetryHistoryDescription}
        </p>

        <div className="max-w-md space-y-4">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="retention">
                {localization.routes.settings.components.settingsPage.packetHistoryLimitLabel}
              </Label>
              <Input
                id="retention"
                type="number"
                min={1}
                max={TELEMETRY_PACKET_RETENTION_LIMIT}
                value={retention}
                onChange={(e) => setRetention(Number(e.target.value))}
                className="h-8 w-24 text-right font-mono"
              />
            </div>
            <Slider
              value={[retention]}
              min={10}
              max={TELEMETRY_PACKET_RETENTION_LIMIT}
              step={10}
              onValueChange={([v]) => setRetention(v)}
            />
            <p className="text-xs text-muted-foreground">
              {localization.routes.settings.components.settingsPage.packetHistoryLimitHint(
                TELEMETRY_PACKET_RETENTION_LIMIT
              )}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
