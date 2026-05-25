import { useMemo, useState } from 'react';
import { Check, ChevronsUpDown, FileText, Folder, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type {
  CalcType,
  CncProgram,
  SpecialParameter,
  TagCalcConfig,
  TagType,
} from '@/types/uniemu';
import { localization } from '@/localization';
import { cn } from '@/lib/utils';
import { getCalcTypeLabel } from './calcLabels';

const sanitizeStaticValue = (tagType: TagType, value: string) => {
  if (tagType === 'int') {
    return value.replace(/[^\d-]/g, '').replace(/(?!^)-/g, '');
  }

  if (tagType === 'double') {
    const normalized = value
      .replace(',', '.')
      .replace(/[^\d.-]/g, '')
      .replace(/(?!^)-/g, '');
    const [whole, ...fractionParts] = normalized.split('.');

    return fractionParts.length === 0 ? whole : `${whole}.${fractionParts.join('')}`;
  }

  return value;
};

interface Props {
  value: TagCalcConfig;
  onChange: (next: TagCalcConfig) => void;
  tagType: TagType;
  specialParameter?: SpecialParameter;
  visibleCncPrograms?: CncProgram[];
  sharedCncPrograms?: CncProgram[];
  emulatorCncPrograms?: CncProgram[];
  calcTypes: readonly CalcType[];
  /** Скрыть поле «Duration» (актуально внутри сегмента сценария — длительность задаётся отдельно). */
  hideDuration?: boolean;
  compact?: boolean;
}

export function CalcConfigFields({
  value,
  onChange,
  tagType,
  specialParameter = 'None',
  visibleCncPrograms = [],
  sharedCncPrograms = [],
  emulatorCncPrograms = [],
  calcTypes,
  hideDuration,
  compact,
}: Props) {
  const [programPickerOpen, setProgramPickerOpen] = useState(false);
  const set = (patch: Partial<TagCalcConfig>) => onChange({ ...value, ...patch });
  const numField = (v: number | undefined) => (v ?? 0).toString();
  const labelCls = compact ? 'text-[11px]' : 'text-xs';
  const inputCls = compact ? 'h-8 font-mono text-xs' : 'font-mono';
  const isStatic = value.type === 'Static';
  const isProgramNameParameter =
    specialParameter === 'PrgName' || specialParameter === 'Subprogram';
  const selectedCncProgram = useMemo(
    () =>
      visibleCncPrograms.find(
        (program) =>
          program.name.localeCompare(value.start ?? '', undefined, { sensitivity: 'accent' }) === 0
      ),
    [value.start, visibleCncPrograms]
  );

  const showStartFinish =
    value.type === 'Line' ||
    value.type === 'Curve' ||
    value.type === 'SquircleEarly' ||
    value.type === 'SquircleLate' ||
    value.type === 'Random' ||
    isStatic ||
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

  const renderStaticInput = () => {
    if (tagType === 'string' && isProgramNameParameter) {
      return renderProgramNamePicker();
    }

    if (tagType === 'bool') {
      return (
        <div className="flex h-8 items-center rounded-md border border-border bg-background px-3">
          <Switch
            checked={value.start === 'true'}
            onCheckedChange={(checked) => set({ start: checked ? 'true' : 'false' })}
          />
        </div>
      );
    }

    return (
      <Input
        value={value.start ?? ''}
        inputMode={tagType === 'int' ? 'numeric' : tagType === 'double' ? 'decimal' : undefined}
        spellCheck={tagType === 'string'}
        onChange={(e) => set({ start: sanitizeStaticValue(tagType, e.target.value) })}
        className={inputCls}
      />
    );
  };

  const renderProgramOption = (program: CncProgram) => (
    <CommandItem
      key={program.id}
      value={`${program.scope}:${program.name}:${program.description}`}
      onSelect={() => {
        set({ start: program.name });
        setProgramPickerOpen(false);
      }}
      className="items-start gap-2 py-2"
    >
      <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <div className="truncate font-mono text-xs">{program.name}</div>
        {program.description && (
          <div className="truncate text-[10px] text-muted-foreground">{program.description}</div>
        )}
      </div>
      <Check
        className={cn(
          'mt-0.5 h-3.5 w-3.5',
          value.start === program.name ? 'opacity-100' : 'opacity-0'
        )}
      />
    </CommandItem>
  );

  const renderProgramNamePicker = () => (
    <Popover open={programPickerOpen} onOpenChange={setProgramPickerOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          className={cn('w-full justify-between gap-2 font-mono text-xs', compact ? 'h-8' : 'h-9')}
        >
          <span className={cn('truncate', !value.start && 'font-sans text-muted-foreground')}>
            {value.start ||
              localization.routes.emulators.components.addTagDrawer.programPickerSelectPlaceholder}
          </span>
          <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[--radix-popover-trigger-width] p-0">
        <Command>
          <CommandInput
            placeholder={
              localization.routes.emulators.components.addTagDrawer.programPickerSearchPlaceholder
            }
          />
          <CommandList
            onWheel={(event) => {
              event.preventDefault();
              event.stopPropagation();
              event.currentTarget.scrollTop += event.deltaY;
            }}
          >
            <CommandEmpty>
              {localization.routes.emulators.components.addTagDrawer.programPickerEmpty}
            </CommandEmpty>
            <CommandGroup>
              <CommandItem
                value="clear-program-selection"
                onSelect={() => {
                  set({ start: '' });
                  setProgramPickerOpen(false);
                }}
                className="items-center gap-2 py-2"
              >
                <X className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="min-w-0 flex-1 truncate text-xs">
                  {
                    localization.routes.emulators.components.addTagDrawer
                      .programPickerClearSelection
                  }
                </span>
                <Check className={cn('h-3.5 w-3.5', !value.start ? 'opacity-100' : 'opacity-0')} />
              </CommandItem>
            </CommandGroup>
            {value.start && !selectedCncProgram && (
              <CommandGroup
                heading={
                  localization.routes.emulators.components.addTagDrawer.programPickerCurrentGroup
                }
              >
                <CommandItem
                  value={`current:${value.start}`}
                  onSelect={() => setProgramPickerOpen(false)}
                  className="items-start gap-2 py-2"
                >
                  <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-mono text-xs">{value.start}</div>
                    <div className="truncate text-[10px] text-muted-foreground">
                      {
                        localization.routes.emulators.components.addTagDrawer
                          .programPickerCurrentMissing
                      }
                    </div>
                  </div>
                  <Check className="mt-0.5 h-3.5 w-3.5" />
                </CommandItem>
              </CommandGroup>
            )}
            {sharedCncPrograms.length > 0 && (
              <CommandGroup
                heading={
                  <span className="flex items-center gap-1.5">
                    <Folder className="h-3 w-3" />
                    {localization.routes.emulators.components.addTagDrawer.programPickerSharedGroup}
                  </span>
                }
              >
                {sharedCncPrograms.map((program) => renderProgramOption(program))}
              </CommandGroup>
            )}
            {emulatorCncPrograms.length > 0 && (
              <CommandGroup
                heading={
                  <span className="flex items-center gap-1.5">
                    <Folder className="h-3 w-3" />
                    {
                      localization.routes.emulators.components.addTagDrawer
                        .programPickerEmulatorGroup
                    }
                  </span>
                }
              >
                {emulatorCncPrograms.map((program) => renderProgramOption(program))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );

  return (
    <div className="space-y-3">
      <div className="space-y-1">
        <Label className={labelCls}>
          {localization.routes.emulators.components.tagScenario.calcConfigFields.formulaTypeLabel}
        </Label>
        <Select value={value.type} onValueChange={(v) => set({ type: v as CalcType })}>
          <SelectTrigger className={compact ? 'h-8' : 'h-9'}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {calcTypes.map((c) => (
              <SelectItem key={c} value={c}>
                {getCalcTypeLabel(c)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {showStartFinish && (
        <div className="grid grid-cols-2 gap-2">
          <div className="space-y-1">
            <Label className={labelCls}>
              {value.type === 'Sequence'
                ? localization.routes.emulators.components.tagScenario.calcConfigFields
                    .sequenceJsonLabel
                : value.type === 'Random'
                  ? localization.routes.emulators.components.addTagDrawer.minLabel
                  : isStatic
                    ? localization.routes.emulators.components.tagScenario.calcConfigFields
                        .valueLabel
                    : localization.routes.emulators.components.addTagDrawer.startLabel}
            </Label>
            {isStatic ? (
              renderStaticInput()
            ) : (
              <Input
                value={value.start ?? ''}
                onChange={(e) => set({ start: e.target.value })}
                className={inputCls}
              />
            )}
          </div>
          {!isStatic && value.type !== 'Sequence' && (
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
              {
                localization.routes.emulators.components.tagScenario.calcConfigFields
                  .periodSecondsLabel
              }
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
            {
              localization.routes.emulators.components.tagScenario.calcConfigFields
                .durationSecondsLabel
            }
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

      {!isStatic && (
        <div className="space-y-1">
          <Label className={labelCls}>
            {
              localization.routes.emulators.components.tagScenario.calcConfigFields
                .distortionPercentLabel
            }
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
      )}
    </div>
  );
}
