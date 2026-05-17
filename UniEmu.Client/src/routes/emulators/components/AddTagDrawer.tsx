import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronsUpDown, FileText, Folder, Pencil, Sparkles } from 'lucide-react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from '@/components/ui/sheet';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { MonacoCsxEditor } from '@/components/MonacoCsxEditor';
import { buildCsxDocumentUri } from '@/components/csx-language-client';
import { ScenarioEditor } from './tag-scenario/ScenarioEditor';
import { getCalcTypeLabel, getTagTypeLabel } from './tag-scenario/calcLabels';
import { defaultSegment } from './tag-scenario/scenarioMath';
import { useUniEmuStore } from '@/store/uniemu-store';
import { SPECIAL_PARAMETER_OPTIONS } from '@/types/uniemu';
import { cn } from '@/lib/utils';
import type {
  CalcType,
  CncProgram,
  EmulatorTag,
  SpecialParameter,
  TagCalcConfig,
  TagFormulaConfig,
  TagIntervalUnit,
  TagScenarioConfig,
  TagSource,
  TagTrigger,
  TagTriggerEvent,
  TagTriggerMode,
  TagType,
} from '@/types/uniemu';
import { localization } from '@/localization';

const SOURCES: { id: TagSource; label: string }[] = [
  { id: 'static', label: localization.routes.emulators.components.addTagDrawer.text1 },
  { id: 'generator', label: localization.routes.emulators.components.addTagDrawer.text2 },
  { id: 'scenario', label: localization.routes.emulators.components.addTagDrawer.text3 },
  { id: 'formulaScript', label: localization.routes.emulators.components.addTagDrawer.text4 },
  { id: 'script', label: localization.routes.emulators.components.addTagDrawer.text5 },
  // { id: 'cnc', label: localization.routes.emulators.components.addTagDrawer.text6 },
];

const TAG_TYPES: TagType[] = ['int', 'double', 'string', 'bool'];

const CALC_TYPES: CalcType[] = [
  'None',
  'Text',
  'Line',
  'Curve',
  // 'Sequence',
  'Random',
  'Sinusoid',
  'Square',
  'Sawtooth',
  'SquircleEarly',
  'SquircleLate',
];

const DEFAULT_INLINE = 'return 0;\n';

const sanitizeStaticValue = (type: TagType, value: string) => {
  if (type === 'int') {
    return value
      .replace(/[^\d-]/g, '')
      .replace(/(?!^)-/g, '');
  }

  if (type === 'double') {
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
  emulatorId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Когда передан - drawer работает в режиме редактирования. */
  tag?: EmulatorTag | null;
}

export function AddTagDrawer({ emulatorId, open, onOpenChange, tag }: Props) {
  const addTag = useUniEmuStore((s) => s.addTag);
  const updateTag = useUniEmuStore((s) => s.updateTag);
  const scripts = useUniEmuStore((s) => s.scripts);
  const cncPrograms = useUniEmuStore((s) => s.cncPrograms);

  const isEdit = !!tag;

  const [key, setKey] = useState<string>('');
  const [specialParameter, setSpecialParameter] = useState<SpecialParameter>('None');
  const [name, setName] = useState('');
  const [type, setType] = useState<TagType>('string');
  const [source, setSource] = useState<TagSource>('static');
  const [staticValue, setStaticValue] = useState('');
  const [description, setDescription] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [roundEnabled, setRoundEnabled] = useState(false);
  const [roundDigits, setRoundDigits] = useState(2);
  const [initialSnapshot, setInitialSnapshot] = useState('');
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const [programPickerOpen, setProgramPickerOpen] = useState(false);

  // trigger
  const [triggerMode, setTriggerMode] = useState<TagTriggerMode>('interval');
  const [triggerEvent, setTriggerEvent] = useState<TagTriggerEvent>('onStart');
  const [cron, setCron] = useState('0 0 * * *');
  const [intervalValue, setIntervalValue] = useState(1);
  const [intervalUnit, setIntervalUnit] = useState<TagIntervalUnit>('sec');

  // calc
  const [calcType, setCalcType] = useState<CalcType>('Line');
  const [calcStart, setCalcStart] = useState('0');
  const [calcFinish, setCalcFinish] = useState('100');
  const [calcDuration, setCalcDuration] = useState(60);
  const [calcAmplitude, setCalcAmplitude] = useState(1);
  const [calcPeriod, setCalcPeriod] = useState(10);
  const [calcCurvature, setCalcCurvature] = useState(2);
  const [calcDistortion, setCalcDistortion] = useState(0);

  // formula / script
  const [scriptId, setScriptId] = useState<string>('');
  const [inlineScript, setInlineScript] = useState<string>(DEFAULT_INLINE);

  // вложенный drawer редактора Monaco
  const [editorOpen, setEditorOpen] = useState(false);
  const [editorDraft, setEditorDraft] = useState<string>(DEFAULT_INLINE);
  const [editorConfirmCloseOpen, setEditorConfirmCloseOpen] = useState(false);

  // scenario
  const [scenario, setScenario] = useState<TagScenarioConfig>({
    segments: [defaultSegment()],
    continueOnFormulaEnd: 'Repeat',
  });

  const availableScripts = useMemo(
    () => scripts.filter((s) => s.scope === 'shared' || s.emulatorId === emulatorId),
    [scripts, emulatorId]
  );
  const visibleCncPrograms = useMemo(
    () => [...cncPrograms
      .filter((program) =>
        program.scope === 'shared' ||
        (program.scope === 'emulator' && program.emulatorId === emulatorId))]
      .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' })),
    [cncPrograms, emulatorId]
  );
  const sharedCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'shared'),
    [visibleCncPrograms]
  );
  const emulatorCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'emulator'),
    [visibleCncPrograms]
  );

  const reset = () => {
    const nextScenario = { segments: [defaultSegment()], continueOnFormulaEnd: 'Repeat' as const };
    setKey('');
    setSpecialParameter('None');
    setName('');
    setType('string');
    setSource('static');
    setStaticValue('');
    setDescription('');
    setEnabled(true);
    setRoundEnabled(false);
    setRoundDigits(2);
    setTriggerMode('interval');
    setTriggerEvent('onStart');
    setCron('0 0 * * *');
    setIntervalValue(1);
    setIntervalUnit('sec');
    setCalcType('Line');
    setCalcStart('0');
    setCalcFinish('100');
    setCalcDuration(60);
    setCalcAmplitude(1);
    setCalcPeriod(10);
    setCalcCurvature(2);
    setCalcDistortion(0);
    setScriptId('');
    setInlineScript(DEFAULT_INLINE);
    setScenario(nextScenario);
    return nextScenario;
  };

  const buildSnapshot = () =>
    JSON.stringify({
      key,
      specialParameter,
      name,
      type,
      source,
      staticValue,
      description,
      enabled,
      roundDigits:
        type === 'double' && roundEnabled
          ? Math.max(0, Math.min(15, Math.round(roundDigits)))
          : null,
      triggerMode,
      triggerEvent,
      cron,
      intervalValue,
      intervalUnit,
      calcType,
      calcStart,
      calcFinish,
      calcDuration,
      calcAmplitude,
      calcPeriod,
      calcCurvature,
      calcDistortion,
      scriptId,
      inlineScript,
      scenario,
    });

  // При открытии в режиме edit - гидратируем поля из тега.
  useEffect(() => {
    if (!open) return;
    if (tag) {
      setKey(tag.key);
      setSpecialParameter(tag.specialParameter ?? 'None');
      setName(tag.name);
      setType(tag.type);
      setSource(tag.source);
      setStaticValue(tag.source === 'static' ? tag.preview : '');
      setDescription(tag.description ?? '');
      setEnabled(tag.enabled ?? true);
      setRoundEnabled(
        tag.type === 'double' && tag.roundDigits !== null && tag.roundDigits !== undefined
      );
      setRoundDigits(tag.roundDigits ?? 2);
      setTriggerMode(tag.trigger.mode);
      setTriggerEvent(tag.trigger.event ?? 'onStart');
      setCron(tag.trigger.cron ?? '0 0 * * *');
      setIntervalValue(tag.trigger.intervalValue ?? 1);
      setIntervalUnit(tag.trigger.intervalUnit ?? 'sec');
      if (tag.calc) {
        setCalcType(tag.calc.type);
        setCalcStart(tag.calc.start ?? '0');
        setCalcFinish(tag.calc.finish ?? '100');
        setCalcDuration(tag.calc.duration ?? 60);
        setCalcAmplitude(tag.calc.amplitude ?? 1);
        setCalcPeriod(tag.calc.period ?? 10);
        setCalcCurvature(tag.calc.curvature ?? 2);
        setCalcDistortion(tag.calc.distortion ?? 0);
      }
      setScriptId(tag.formula?.scriptId ?? '');
      setInlineScript(tag.formula?.inlineScript ?? DEFAULT_INLINE);
      setScenario(tag.scenario ?? { segments: [defaultSegment()], continueOnFormulaEnd: 'Repeat' });
      setInitialSnapshot(
        JSON.stringify({
          key: tag.key,
          specialParameter: tag.specialParameter ?? 'None',
          name: tag.name,
          type: tag.type,
          source: tag.source,
          staticValue: tag.source === 'static' ? tag.preview : '',
          description: tag.description ?? '',
          enabled: tag.enabled ?? true,
          roundDigits: tag.type === 'double' ? (tag.roundDigits ?? null) : null,
          triggerMode: tag.trigger.mode,
          triggerEvent: tag.trigger.event ?? 'onStart',
          cron: tag.trigger.cron ?? '0 0 * * *',
          intervalValue: tag.trigger.intervalValue ?? 1,
          intervalUnit: tag.trigger.intervalUnit ?? 'sec',
          calcType: tag.calc?.type ?? 'Line',
          calcStart: tag.calc?.start ?? '0',
          calcFinish: tag.calc?.finish ?? '100',
          calcDuration: tag.calc?.duration ?? 60,
          calcAmplitude: tag.calc?.amplitude ?? 1,
          calcPeriod: tag.calc?.period ?? 10,
          calcCurvature: tag.calc?.curvature ?? 2,
          calcDistortion: tag.calc?.distortion ?? 0,
          scriptId: tag.formula?.scriptId ?? '',
          inlineScript: tag.formula?.inlineScript ?? DEFAULT_INLINE,
          scenario: tag.scenario ?? {
            segments: [defaultSegment()],
            continueOnFormulaEnd: 'Repeat',
          },
        })
      );
    } else {
      const nextScenario = reset();
      setInitialSnapshot(
        JSON.stringify({
          key: '',
          specialParameter: 'None',
          name: '',
          type: 'string',
          source: 'static',
          staticValue: '',
          description: '',
          enabled: true,
          roundDigits: null,
          triggerMode: 'interval',
          triggerEvent: 'onStart',
          cron: '0 0 * * *',
          intervalValue: 1,
          intervalUnit: 'sec',
          calcType: 'Line',
          calcStart: '0',
          calcFinish: '100',
          calcDuration: 60,
          calcAmplitude: 1,
          calcPeriod: 10,
          calcCurvature: 2,
          calcDistortion: 0,
          scriptId: '',
          inlineScript: DEFAULT_INLINE,
          scenario: nextScenario,
        })
      );
    }
  }, [open, tag]);

  const isScenario = source === 'scenario';
  const isProgramNameParameter = specialParameter === 'PrgName' || specialParameter === 'Subprogram';
  const canSubmit =
    name.trim().length > 0 && (!isScenario || scenario.segments.some((s) => s.duration > 0));
  const currentSnapshot = buildSnapshot();
  const isDirty = open && initialSnapshot.length > 0 && currentSnapshot !== initialSnapshot;

  const closeWithoutSaving = () => {
    setConfirmCloseOpen(false);
    reset();
    onOpenChange(false);
  };

  const requestClose = () => {
    if (isDirty) {
      setConfirmCloseOpen(true);
      return;
    }

    closeWithoutSaving();
  };

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      onOpenChange(true);
      return;
    }

    requestClose();
  };

  const handleSubmit = async () => {
    if (!canSubmit) return;

    // Для сценария триггер фиксируем как onStart - проигрывание по таймлайну.
    const trigger: TagTrigger = isScenario
      ? { mode: 'once', event: 'onStart' }
      : { mode: triggerMode };
    if (!isScenario) {
      if (triggerMode === 'once') trigger.event = triggerEvent;
      if (triggerMode === 'cron') trigger.cron = cron;
      if (triggerMode === 'interval') {
        trigger.intervalValue = intervalValue;
        trigger.intervalUnit = intervalUnit;
      }
    }

    let calc: TagCalcConfig | undefined;
    if (source === 'generator' || source === 'formula' || source === 'formulaScript') {
      calc = {
        type: calcType,
        start: calcStart,
        finish: calcFinish,
        duration: calcDuration,
        amplitude: calcAmplitude,
        period: calcPeriod,
        curvature: calcCurvature,
        distortion: calcDistortion,
      };
    }

    let formula: TagFormulaConfig | undefined;
    if (source === 'formula' || source === 'script' || source === 'formulaScript') {
      formula = scriptId ? { scriptId } : { inlineScript };
    }

    const preview =
      source === 'static'
        ? staticValue
        : source === 'cnc'
          ? localization.routes.emulators.components.addTagDrawer.text7
          : isScenario
            ? localization.routes.emulators.components.addTagDrawer.text8
            : calc?.type === 'Text'
              ? calcStart
              : localization.routes.emulators.components.addTagDrawer.text9;
    const normalizedPreview =
      source === 'static' && type === 'bool'
        ? staticValue === 'true' ? 'true' : 'false'
        : preview;

    const payload: Omit<EmulatorTag, 'id'> = {
      name: name.trim(),
      key,
      type,
      source,
      preview: normalizedPreview,
      trigger,
      calc,
      formula,
      scenario: isScenario ? scenario : undefined,
      specialParameter: specialParameter !== 'None' ? specialParameter : undefined,
      description: description.trim() || undefined,
      enabled,
      roundDigits:
        type === 'double' && roundEnabled
          ? Math.max(0, Math.min(15, Math.round(roundDigits)))
          : null,
    };

    if (isEdit && tag) {
      await updateTag(emulatorId, tag.id, payload);
    } else {
      await addTag(emulatorId, payload);
    }
    reset();
    onOpenChange(false);
  };

  const showCalc =
    source === 'generator' || source === 'formula' || source === 'formulaScript';
  const showScript =
    source === 'formula' || source === 'script' || source === 'formulaScript';
  const showTrigger = !isScenario;
  const useInlineScript = !scriptId;
  const isEditorDirty = editorOpen && editorDraft !== inlineScript;
  const selectedCncProgram = visibleCncPrograms.find(
    (program) => program.name.localeCompare(staticValue, undefined, { sensitivity: 'accent' }) === 0
  );
  const inlineDocumentUri = buildCsxDocumentUri({
    id: tag?.id ?? 'new-inline',
    name: `inline/${tag?.id ?? 'new-tag'}.csx`,
    scope: 'emulator',
    emulatorId,
  });

  const openEditor = () => {
    setEditorDraft(inlineScript);
    setEditorOpen(true);
  };

  const closeEditorWithoutSaving = () => {
    setEditorConfirmCloseOpen(false);
    setEditorDraft(inlineScript);
    setEditorOpen(false);
  };

  const requestEditorClose = () => {
    if (isEditorDirty) {
      setEditorConfirmCloseOpen(true);
      return;
    }

    closeEditorWithoutSaving();
  };

  const handleEditorOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setEditorOpen(true);
      return;
    }

    requestEditorClose();
  };

  const applyEditorDraft = () => {
    setInlineScript(editorDraft);
    setEditorOpen(false);
  };

  const renderProgramOption = (program: CncProgram) => (
    <CommandItem
      key={program.id}
      value={`${program.scope}:${program.name}:${program.description}`}
      onSelect={() => {
        setStaticValue(program.name);
        setProgramPickerOpen(false);
      }}
      className="items-start gap-2 py-2"
    >
      <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <div className="truncate font-mono text-xs">{program.name}</div>
        {program.description && (
          <div className="truncate text-[10px] text-muted-foreground">
            {program.description}
          </div>
        )}
      </div>
      <Check
        className={cn(
          'mt-0.5 h-3.5 w-3.5',
          staticValue === program.name ? 'opacity-100' : 'opacity-0'
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
          className="h-9 w-full justify-between gap-2 font-mono text-xs"
        >
          <span className={cn('truncate', !staticValue && 'font-sans text-muted-foreground')}>
            {staticValue || localization.routes.emulators.components.addTagDrawer.programPickerSelectPlaceholder}
          </span>
          <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[--radix-popover-trigger-width] p-0">
        <Command>
          <CommandInput
            placeholder={localization.routes.emulators.components.addTagDrawer.programPickerSearchPlaceholder}
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
            {staticValue && !selectedCncProgram && (
              <CommandGroup
                heading={localization.routes.emulators.components.addTagDrawer.programPickerCurrentGroup}
              >
                <CommandItem
                  value={`current:${staticValue}`}
                  onSelect={() => setProgramPickerOpen(false)}
                  className="items-start gap-2 py-2"
                >
                  <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-mono text-xs">{staticValue}</div>
                    <div className="truncate text-[10px] text-muted-foreground">
                      {localization.routes.emulators.components.addTagDrawer.programPickerCurrentMissing}
                    </div>
                  </div>
                  <Check className="mt-0.5 h-3.5 w-3.5" />
                </CommandItem>
              </CommandGroup>
            )}
            {sharedCncPrograms.length > 0 && (
              <CommandGroup
                heading={(
                  <span className="flex items-center gap-1.5">
                    <Folder className="h-3 w-3" />
                    {localization.routes.emulators.components.addTagDrawer.programPickerSharedGroup}
                  </span>
                )}
              >
                {sharedCncPrograms.map((program) => renderProgramOption(program))}
              </CommandGroup>
            )}
            {emulatorCncPrograms.length > 0 && (
              <CommandGroup
                heading={(
                  <span className="flex items-center gap-1.5">
                    <Folder className="h-3 w-3" />
                    {localization.routes.emulators.components.addTagDrawer.programPickerEmulatorGroup}
                  </span>
                )}
              >
                {emulatorCncPrograms.map((program) => renderProgramOption(program))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );

  const renderStaticValueInput = () => {
    if (type === 'string' && isProgramNameParameter) {
      return renderProgramNamePicker();
    }

    if (type === 'bool') {
      return (
        <div className="flex h-9 items-center rounded-md border border-border bg-background px-3">
          <Switch
            checked={staticValue === 'true'}
            onCheckedChange={(checked) => setStaticValue(checked ? 'true' : 'false')}
          />
        </div>
      );
    }

    return (
      <Input
        value={staticValue}
        inputMode={type === 'int' ? 'numeric' : type === 'double' ? 'decimal' : undefined}
        spellCheck={type === 'string'}
        onChange={(e) => setStaticValue(sanitizeStaticValue(type, e.target.value))}
        className="font-mono"
      />
    );
  };

  return (
    <>
      <Sheet open={open} onOpenChange={handleOpenChange}>
        <SheetContent
          onEscapeKeyDown={(event) => {
            if (isEdit) {
              event.preventDefault();
            }
          }}
          className={
            isScenario
              ? 'w-full overflow-y-auto sm:max-w-3xl lg:max-w-5xl xl:max-w-6xl'
              : 'w-full overflow-y-auto sm:max-w-lg'
          }
        >
          <SheetHeader>
            <SheetTitle>
              {isEdit
                ? localization.routes.emulators.components.addTagDrawer.text10
                : localization.routes.emulators.components.addTagDrawer.text11}
            </SheetTitle>
            {/* <SheetDescription>
              {localization.routes.emulators.components.addTagDrawer.text12}
            </SheetDescription> */}
          </SheetHeader>

          <div className="space-y-5 py-6">
            {/* Базовые параметры */}
            <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
              <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                {localization.routes.emulators.components.addTagDrawer.text13}
              </h4>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text14}
                  </Label>
                  <Input
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="MyTag"
                    className="font-mono"
                  />
                  <p className="text-[10px] text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text15}
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.parameterKeyLabel}
                  </Label>
                  <Input
                    value={key}
                    spellCheck={false}
                    onChange={(e) => setKey(e.target.value)}
                    placeholder="my_param"
                    className="font-mono"
                  />
                  <p className="text-[10px] text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text16}
                  </p>
                </div>
              </div>

              {/* Специализированный параметр (UniEmu protocol) */}
              <div className="space-y-1.5">
                <Label className="text-xs flex items-center gap-1.5">
                  <Sparkles className="h-3 w-3 text-primary" />
                  {localization.routes.emulators.components.addTagDrawer.specialParameterLabel}
                  <span className="text-[10px] font-normal text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text17}
                  </span>
                </Label>
                <Select
                  value={specialParameter}
                  onValueChange={(v) => setSpecialParameter(v as SpecialParameter)}
                >
                  <SelectTrigger className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {SPECIAL_PARAMETER_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value} className="text-xs">
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {/* <p className="text-[10px] text-muted-foreground">
                  {localization.routes.emulators.components.addTagDrawer.text18}
                </p> */}
              </div>

              <div className="space-y-1.5">
                <Label className="text-xs">
                  {localization.routes.emulators.components.addTagDrawer.dataTypeLabel}
                </Label>
                <Select value={type} onValueChange={(v) => setType(v as TagType)}>
                  <SelectTrigger className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {TAG_TYPES.map((tagType) => (
                      <SelectItem key={tagType} value={tagType}>
                        {getTagTypeLabel(tagType)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {type === 'double' && (
                <div className="flex items-center justify-between gap-3 rounded-md border border-border bg-background/40 px-3 py-2">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text19}
                  </Label>
                  <div className="flex shrink-0 items-center gap-2">
                    <Switch checked={roundEnabled} onCheckedChange={setRoundEnabled} />
                    <Input
                      type="number"
                      min={0}
                      max={15}
                      step={1}
                      value={roundDigits}
                      disabled={!roundEnabled}
                      onChange={(e) =>
                        setRoundDigits(Math.max(0, Math.min(15, Number(e.target.value) || 0)))
                      }
                      className="h-8 w-20 font-mono"
                    />
                    <span className="text-xs text-muted-foreground">
                      {localization.routes.emulators.components.addTagDrawer.text20}
                    </span>
                  </div>
                </div>
              )}

              <div className="space-y-1.5">
                <Label className="text-xs">
                  {localization.routes.emulators.components.addTagDrawer.text21}
                </Label>
                <Select value={source} onValueChange={(v) => setSource(v as TagSource)}>
                  <SelectTrigger className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {SOURCES.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {source === 'static' && (
                <div className="space-y-1.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text22}
                  </Label>
                  {renderStaticValueInput()}
                </div>
              )}

              <div className="space-y-1.5">
                <Label className="text-xs">
                  {localization.routes.emulators.components.addTagDrawer.text23}
                </Label>
                <Textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={2}
                />
              </div>

              <div className="flex items-start justify-between gap-3 rounded-md border border-border bg-background/40 p-2.5">
                <div className="space-y-0.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text24}
                  </Label>
                  <p className="text-[10px] text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text25}
                  </p>
                </div>
                <Switch checked={enabled} onCheckedChange={setEnabled} />
              </div>
            </section>

            {/* Сценарий */}
            {isScenario && (
              <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
                <div className="flex items-baseline justify-between">
                  <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text26}
                  </h4>
                  <span className="text-[10px] text-muted-foreground">
                    {localization.routes.emulators.components.addTagDrawer.text27}
                  </span>
                </div>
                <ScenarioEditor value={scenario} onChange={setScenario} />
              </section>
            )}

            {/* Триггер */}
            {showTrigger && (
              <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.addTagDrawer.text28}
                </h4>
                <Select
                  value={triggerMode}
                  onValueChange={(v) => setTriggerMode(v as TagTriggerMode)}
                >
                  <SelectTrigger className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="once">
                      {localization.routes.emulators.components.addTagDrawer.text29}
                    </SelectItem>
                    <SelectItem value="cron">
                      {localization.routes.emulators.components.addTagDrawer.text30}
                    </SelectItem>
                    <SelectItem value="interval">
                      {localization.routes.emulators.components.addTagDrawer.text31}
                    </SelectItem>
                  </SelectContent>
                </Select>

                {triggerMode === 'once' && (
                  <div className="space-y-1.5">
                    <Label className="text-xs">
                      {localization.routes.emulators.components.addTagDrawer.text32}
                    </Label>
                    <Select
                      value={triggerEvent}
                      onValueChange={(v) => setTriggerEvent(v as TagTriggerEvent)}
                    >
                      <SelectTrigger className="h-9">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="onStart">
                          {localization.routes.emulators.components.addTagDrawer.text33}
                        </SelectItem>
                        <SelectItem value="onStop">
                          {localization.routes.emulators.components.addTagDrawer.text34}
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                )}

                {triggerMode === 'cron' && (
                  <div className="space-y-1.5">
                    <Label className="text-xs">
                      {localization.routes.emulators.components.addTagDrawer.text35}
                    </Label>
                    <Input
                      value={cron}
                      spellCheck={false}
                      onChange={(e) => setCron(e.target.value)}
                      placeholder="0 0 * * *"
                      className="font-mono"
                    />
                    <p className="text-[11px] text-muted-foreground">
                      {localization.routes.emulators.components.addTagDrawer.text36}
                      <code className="font-mono">0 0 * * *</code>{' '}
                      {localization.routes.emulators.components.addTagDrawer.text37}
                    </p>
                  </div>
                )}

                {triggerMode === 'interval' && (
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1.5">
                      <Label className="text-xs">
                        {localization.routes.emulators.components.addTagDrawer.text38}
                      </Label>
                      <Input
                        type="number"
                        min={1}
                        value={intervalValue}
                        onChange={(e) => setIntervalValue(Math.max(1, Number(e.target.value) || 1))}
                        className="font-mono"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label className="text-xs">
                        {localization.routes.emulators.components.addTagDrawer.text39}
                      </Label>
                      <Select
                        value={intervalUnit}
                        onValueChange={(v) => setIntervalUnit(v as TagIntervalUnit)}
                      >
                        <SelectTrigger className="h-9">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="ms">
                            {localization.routes.emulators.components.addTagDrawer.text40}
                          </SelectItem>
                          <SelectItem value="sec">
                            {localization.routes.emulators.components.addTagDrawer.text41}
                          </SelectItem>
                          <SelectItem value="min">
                            {localization.routes.emulators.components.addTagDrawer.text42}
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                )}
              </section>
            )}

            {/* Calc */}
            {showCalc && (
              <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.addTagDrawer.text43}
                </h4>
                <Select value={calcType} onValueChange={(v) => setCalcType(v as CalcType)}>
                  <SelectTrigger className="h-9">
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

                {(calcType === 'Line' ||
                  calcType === 'Curve' ||
                  calcType === 'SquircleEarly' ||
                  calcType === 'SquircleLate' ||
                  calcType === 'Random' ||
                  calcType === 'Text' ||
                  calcType === 'Sequence') && (
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1.5">
                      <Label className="text-xs">
                        {calcType === 'Sequence'
                          ? localization.routes.emulators.components.addTagDrawer.text44
                          : localization.routes.emulators.components.addTagDrawer.startLabel}
                      </Label>
                      <Input
                        value={calcStart}
                        spellCheck={calcType === 'Text' || calcType === 'Sequence'}
                        onChange={(e) => setCalcStart(e.target.value)}
                        className="font-mono"
                      />
                    </div>
                    {calcType !== 'Text' && calcType !== 'Sequence' && (
                      <div className="space-y-1.5">
                        <Label className="text-xs">
                          {localization.routes.emulators.components.addTagDrawer.finishLabel}
                        </Label>
                        <Input
                          value={calcFinish}
                          spellCheck={false}
                          onChange={(e) => setCalcFinish(e.target.value)}
                          className="font-mono"
                        />
                      </div>
                    )}
                  </div>
                )}

                {(calcType === 'Line' ||
                  calcType === 'Curve' ||
                  calcType === 'Sequence' ||
                  calcType === 'SquircleEarly' ||
                  calcType === 'SquircleLate') && (
                  <div className="space-y-1.5">
                    <Label className="text-xs">
                      {localization.routes.emulators.components.addTagDrawer.text45}
                    </Label>
                    <Input
                      type="number"
                      min={0}
                      value={calcDuration}
                      onChange={(e) => setCalcDuration(Number(e.target.value) || 0)}
                      className="font-mono"
                    />
                  </div>
                )}

                {(calcType === 'Sinusoid' || calcType === 'Square' || calcType === 'Sawtooth') && (
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1.5">
                      <Label className="text-xs">
                        {localization.routes.emulators.components.addTagDrawer.amplitudeLabel}
                      </Label>
                      <Input
                        type="number"
                        value={calcAmplitude}
                        onChange={(e) => setCalcAmplitude(Number(e.target.value) || 0)}
                        className="font-mono"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label className="text-xs">
                        {localization.routes.emulators.components.addTagDrawer.text46}
                      </Label>
                      <Input
                        type="number"
                        value={calcPeriod}
                        onChange={(e) => setCalcPeriod(Number(e.target.value) || 0)}
                        className="font-mono"
                      />
                    </div>
                  </div>
                )}

                {calcType === 'Curve' && (
                  <div className="space-y-1.5">
                    <Label className="text-xs">
                      {localization.routes.emulators.components.addTagDrawer.curvatureLabel}
                    </Label>
                    <Input
                      type="number"
                      value={calcCurvature}
                      onChange={(e) => setCalcCurvature(Number(e.target.value) || 0)}
                      className="font-mono"
                    />
                  </div>
                )}

                <div className="space-y-1.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text47}
                  </Label>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={calcDistortion}
                    onChange={(e) => setCalcDistortion(Number(e.target.value) || 0)}
                    className="font-mono"
                  />
                </div>
              </section>
            )}

            {/* Script */}
            {showScript && (
              <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.addTagDrawer.text48}
                </h4>
                <div className="space-y-1.5">
                  <Label className="text-xs">
                    {localization.routes.emulators.components.addTagDrawer.text49}
                  </Label>
                  <Select
                    value={scriptId || '__inline__'}
                    onValueChange={(v) => setScriptId(v === '__inline__' ? '' : v)}
                  >
                    <SelectTrigger className="h-9">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__inline__">
                        {localization.routes.emulators.components.addTagDrawer.text50}
                      </SelectItem>
                      {availableScripts.map((s) => (
                        <SelectItem key={s.id} value={s.id} className="font-mono text-xs">
                          {s.scope === 'shared' ? 'shared' : 'local'} · {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {useInlineScript && (
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <Label className="text-xs">
                        {localization.routes.emulators.components.addTagDrawer.text51}
                      </Label>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 gap-1.5 text-xs"
                        onClick={openEditor}
                      >
                        <Pencil className="h-3 w-3" />{' '}
                        {localization.routes.emulators.components.addTagDrawer.text52}
                      </Button>
                    </div>
                    {/* Read-only Monaco preview */}
                    <div className="h-44 overflow-hidden rounded-md border border-border">
                      <MonacoCsxEditor
                        value={inlineScript}
                        onChange={() => {}}
                        minimap={false}
                        readOnly
                      />
                    </div>
                  </div>
                )}
              </section>
            )}
          </div>

          <SheetFooter className="gap-2">
            <Button variant="outline" onClick={requestClose}>
              {localization.routes.emulators.components.addTagDrawer.text53}
            </Button>
            <Button onClick={() => void handleSubmit()} disabled={!canSubmit}>
              {isEdit
                ? localization.routes.emulators.components.addTagDrawer.text54
                : localization.routes.emulators.components.addTagDrawer.text55}
            </Button>
          </SheetFooter>

          {/* Вложенный drawer 2-го уровня - полноценный Monaco-редактор */}
          <Sheet open={editorOpen} onOpenChange={handleEditorOpenChange}>
            <SheetContent
              side="right"
              onEscapeKeyDown={(event) => event.preventDefault()}
              onInteractOutside={(event) => event.preventDefault()}
              className="flex w-full flex-col gap-0 p-0 sm:max-w-3xl"
            >
              <SheetHeader className="border-b border-border px-6 py-4">
                <SheetTitle>
                  {localization.routes.emulators.components.addTagDrawer.text56}
                </SheetTitle>
                {/* <SheetDescription>
                  {localization.routes.emulators.components.addTagDrawer.text57}
                </SheetDescription> */}
              </SheetHeader>
              <div className="flex-1 overflow-hidden">
                <MonacoCsxEditor
                  value={editorDraft}
                  onChange={setEditorDraft}
                  documentUri={inlineDocumentUri}
                />
              </div>
              <SheetFooter className="border-t border-border px-6 py-4">
                <Button variant="outline" onClick={requestEditorClose}>
                  {localization.routes.emulators.components.addTagDrawer.text58}
                </Button>
                <Button onClick={applyEditorDraft}>
                  {localization.routes.emulators.components.addTagDrawer.text59}
                </Button>
              </SheetFooter>
            </SheetContent>
          </Sheet>
        </SheetContent>
      </Sheet>
      <AlertDialog open={confirmCloseOpen} onOpenChange={setConfirmCloseOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.addTagDrawer.text60}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.addTagDrawer.text61}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.addTagDrawer.text62}
            </AlertDialogCancel>
            <AlertDialogAction onClick={closeWithoutSaving}>
              {localization.routes.emulators.components.addTagDrawer.text63}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <AlertDialog open={editorConfirmCloseOpen} onOpenChange={setEditorConfirmCloseOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.addTagDrawer.text64}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.addTagDrawer.text65}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.addTagDrawer.text66}
            </AlertDialogCancel>
            <AlertDialogAction onClick={closeEditorWithoutSaving}>
              {localization.routes.emulators.components.addTagDrawer.text67}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
