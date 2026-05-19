import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { CalcType, TagCalcConfig } from '@/types/uniemu';
import { localization } from '@/localization';
import { getCalcTypeLabel } from './calcLabels';

const CALC_TYPES: CalcType[] = [
  'None',
  'Text',
  'Line',
  'Curve',
  'Sequence',
  'Random',
  'Sinusoid',
  'Square',
  'Sawtooth',
  'SquircleEarly',
  'SquircleLate',
];

interface Props {
  value: TagCalcConfig;
  onChange: (next: TagCalcConfig) => void;
  /** Скрыть поле «Duration» (актуально внутри сегмента сценария — длительность задаётся отдельно). */
  hideDuration?: boolean;
  compact?: boolean;
}

export function CalcConfigFields({ value, onChange, hideDuration, compact }: Props) {
  const set = (patch: Partial<TagCalcConfig>) => onChange({ ...value, ...patch });
  const numField = (v: number | undefined) => (v ?? 0).toString();
  const labelCls = compact ? 'text-[11px]' : 'text-xs';
  const inputCls = compact ? 'h-8 font-mono text-xs' : 'font-mono';

  const showStartFinish =
    value.type === 'Line' ||
    value.type === 'Curve' ||
    value.type === 'SquircleEarly' ||
    value.type === 'SquircleLate' ||
    value.type === 'Random' ||
    value.type === 'Text' ||
    value.type === 'Sequence';

  const showDurationField =
    !hideDuration &&
    (value.type === 'Line' ||
      value.type === 'Curve' ||
      value.type === 'Sequence' ||
      value.type === 'SquircleEarly' ||
      value.type === 'SquircleLate');

  const showWaveFields =
    value.type === 'Sinusoid' || value.type === 'Square' || value.type === 'Sawtooth';

  return (
    <div className="space-y-3">
      <Select value={value.type} onValueChange={(v) => set({ type: v as CalcType })}>
        <SelectTrigger className={compact ? 'h-8' : 'h-9'}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {CALC_TYPES.map((c) => (
            <SelectItem key={c} value={c}>
              {getCalcTypeLabel(c)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {showStartFinish && (
        <div className="grid grid-cols-2 gap-2">
          <div className="space-y-1">
            <Label className={labelCls}>
              {value.type === 'Sequence'
                ? localization.routes.emulators.components.tagScenario.calcConfigFields.sequenceJsonLabel
                : value.type === 'Random'
                  ? localization.routes.emulators.components.addTagDrawer.minLabel
                  : localization.routes.emulators.components.addTagDrawer.startLabel}
            </Label>
            <Input
              value={value.start ?? ''}
              onChange={(e) => set({ start: e.target.value })}
              className={inputCls}
            />
          </div>
          {value.type !== 'Text' && value.type !== 'Sequence' && (
            <div className="space-y-1">
              <Label className={labelCls}>
                {value.type === 'Random'
                  ? localization.routes.emulators.components.addTagDrawer.maxLabel
                  : localization.routes.emulators.components.addTagDrawer.finishLabel}
              </Label>
              <Input
                value={value.finish ?? ''}
                onChange={(e) => set({ finish: e.target.value })}
                className={inputCls}
              />
            </div>
          )}
        </div>
      )}

      {showWaveFields && (
        <div className="grid grid-cols-3 gap-2">
          <div className="space-y-1">
            <Label className={labelCls}>
              {localization.routes.emulators.components.addTagDrawer.centerLabel}
            </Label>
            <Input
              type="number"
              value={numField(Number(value.start ?? 0))}
              onChange={(e) => set({ start: e.target.value })}
              className={inputCls}
            />
          </div>
          <div className="space-y-1">
            <Label className={labelCls}>
              {localization.routes.emulators.components.addTagDrawer.amplitudeLabel}
            </Label>
            <Input
              type="number"
              value={numField(value.amplitude)}
              onChange={(e) => set({ amplitude: Number(e.target.value) || 0 })}
              className={inputCls}
            />
          </div>
          <div className="space-y-1">
            <Label className={labelCls}>
              {localization.routes.emulators.components.tagScenario.calcConfigFields.periodSecondsLabel}
            </Label>
            <Input
              type="number"
              min={1}
              value={numField(value.period)}
              onChange={(e) => set({ period: Math.max(1, Number(e.target.value) || 1) })}
              className={inputCls}
            />
          </div>
        </div>
      )}

      {value.type === 'Curve' && (
        <div className="space-y-1">
          <Label className={labelCls}>
            {localization.routes.emulators.components.addTagDrawer.curvatureLabel}
          </Label>
          <Input
            type="number"
            value={numField(value.curvature)}
            onChange={(e) => set({ curvature: Number(e.target.value) || 0 })}
            className={inputCls}
          />
        </div>
      )}

      {showDurationField && (
        <div className="space-y-1">
          <Label className={labelCls}>
            {localization.routes.emulators.components.tagScenario.calcConfigFields.durationSecondsLabel}
          </Label>
          <Input
            type="number"
            min={0}
            value={numField(value.duration)}
            onChange={(e) => set({ duration: Number(e.target.value) || 0 })}
            className={inputCls}
          />
        </div>
      )}

      <div className="space-y-1">
        <Label className={labelCls}>
          {localization.routes.emulators.components.tagScenario.calcConfigFields.distortionPercentLabel}
        </Label>
        <Input
          type="number"
          min={0}
          max={100}
          value={numField(value.distortion)}
          onChange={(e) => set({ distortion: Number(e.target.value) || 0 })}
          className={inputCls}
        />
      </div>
    </div>
  );
}
