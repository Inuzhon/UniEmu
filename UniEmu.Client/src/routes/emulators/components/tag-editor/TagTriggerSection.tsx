import { memo } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { localization } from '@/localization';
import type { TagIntervalUnit, TagTriggerEvent, TagTriggerMode } from '@/types/uniemu';
import type { SetTagEditorField } from './types';

interface Props {
  triggerMode: TagTriggerMode;
  triggerEvent: TagTriggerEvent;
  cron: string;
  intervalValue: number;
  intervalUnit: TagIntervalUnit;
  onFieldChange: SetTagEditorField;
}

export const TagTriggerSection = memo(function TagTriggerSection({
  triggerMode,
  triggerEvent,
  cron,
  intervalValue,
  intervalUnit,
  onFieldChange,
}: Props) {
  return (
    <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
      <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        {localization.routes.emulators.components.addTagDrawer.triggerSectionTitle}
      </h4>
      <Select
        value={triggerMode}
        onValueChange={(value) => onFieldChange('triggerMode', value as TagTriggerMode)}
      >
        <SelectTrigger className="h-9">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="once">
            {localization.routes.emulators.components.addTagDrawer.triggerModeOnceLabel}
          </SelectItem>
          <SelectItem value="interval">
            {localization.routes.emulators.components.addTagDrawer.triggerModeIntervalLabel}
          </SelectItem>
        </SelectContent>
      </Select>

      {triggerMode === 'once' && (
        <div className="space-y-1.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.triggerEventLabel}
          </Label>
          <Select
            value={triggerEvent}
            onValueChange={(value) => onFieldChange('triggerEvent', value as TagTriggerEvent)}
          >
            <SelectTrigger className="h-9">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="onStart">
                {localization.routes.emulators.components.addTagDrawer.triggerEventOnStartLabel}
              </SelectItem>
              <SelectItem value="onStop">
                {localization.routes.emulators.components.addTagDrawer.triggerEventOnStopLabel}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}

      {triggerMode === 'cron' && (
        <div className="space-y-1.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.cronExpressionLabel}
          </Label>
          <Input
            value={cron}
            spellCheck={false}
            onChange={(event) => onFieldChange('cron', event.target.value)}
            placeholder="0 0 * * *"
            className="font-mono"
          />
          <p className="text-[11px] text-muted-foreground">
            {localization.routes.emulators.components.addTagDrawer.cronExamplePrefix}
            <code className="font-mono">0 0 * * *</code>{' '}
            {localization.routes.emulators.components.addTagDrawer.cronDailyMidnightHint}
          </p>
        </div>
      )}

      {triggerMode === 'interval' && (
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs">
              {localization.routes.emulators.components.addTagDrawer.intervalEveryLabel}
            </Label>
            <Input
              type="number"
              min={1}
              value={intervalValue}
              onChange={(event) => onFieldChange('intervalValue', Math.max(1, Number(event.target.value) || 1))}
              className="font-mono"
            />
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs">
              {localization.routes.emulators.components.addTagDrawer.intervalUnitLabel}
            </Label>
            <Select
              value={intervalUnit}
              onValueChange={(value) => onFieldChange('intervalUnit', value as TagIntervalUnit)}
            >
              <SelectTrigger className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ms">
                  {localization.routes.emulators.components.addTagDrawer.millisecondsUnitLabel}
                </SelectItem>
                <SelectItem value="sec">
                  {localization.routes.emulators.components.addTagDrawer.secondsUnitLabel}
                </SelectItem>
                <SelectItem value="min">
                  {localization.routes.emulators.components.addTagDrawer.minutesUnitLabel}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      )}
    </section>
  );
});
