import { Link, useNavigate, useParams, useRouter } from '@tanstack/react-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent, WheelEvent as ReactWheelEvent } from 'react';
import {
  ArrowLeft,
  Check,
  ChevronDown,
  ChevronRight,
  ChevronsUpDown,
  Download,
  FileText,
  Folder,
  PauseCircle,
  Pencil,
  PlayCircle,
  Plus,
  Settings as SettingsIcon,
  StopCircle,
  Trash2,
} from 'lucide-react';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
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
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { useUniEmuStore } from '@/store/uniemu-store';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import { formatNumber, formatUptime } from '@/utils/format';
import { TimeAgo } from '@/components/TimeAgo';
import { AddTagDrawer } from './AddTagDrawer';
import { EditEmulatorDrawer } from './EditEmulatorDrawer';
import { ScenarioPreviewChart, ScenarioSparkline } from './tag-scenario/ScenarioPreviewChart';
import {
  getTelemetryYBaseRange,
  getTelemetryYDomain,
  panTelemetryYViewport,
  zoomTelemetryYViewport,
} from './telemetryChartViewport';
import type { TelemetryYViewport } from './telemetryChartViewport';
import {
  getCalcTypeLabel,
  getContinueOnFormulaEndLabel,
  getTagIntervalUnitLabel,
  getTagSourceLabel,
  getTagTypeLabel,
} from './tag-scenario/calcLabels';
import { formatDuration, totalDuration } from './tag-scenario/scenarioMath';
import { TELEMETRY_LINE_COLORS } from '@/lib/constants';
import { SHOW_TAG_SCENARIO_PREVIEWS } from '@/lib/feature-flags';
import { cn } from '@/lib/utils';
import type { CncProgram, EmulatorTag, TagSource, TagTrigger, TelemetryPoint } from '@/types/uniemu';
import { localization } from '@/localization';

function formatTrigger(t: TagTrigger, source?: TagSource): string {
  if (source === 'scenario')
    return localization.routes.emulators.components.emulatorDetailPage.timelineTriggerLabel;
  if (t.mode === 'once')
    return t.event === 'onStop'
      ? localization.routes.emulators.components.emulatorDetailPage.onStopTriggerLabel
      : localization.routes.emulators.components.emulatorDetailPage.onStartTriggerLabel;
  if (t.mode === 'cron') return `cron: ${t.cron ?? '-'}`;
  return localization.routes.emulators.components.emulatorDetailPage.intervalTriggerLabel(
    t.intervalValue ?? 0,
    getTagIntervalUnitLabel(t.intervalUnit ?? 'sec')
  );
}

const tabs = [
  { id: 'overview', label: localization.routes.emulators.components.emulatorDetailPage.overviewTabLabel },
  { id: 'tags', label: localization.routes.emulators.components.emulatorDetailPage.tagsTabLabel },
  { id: 'monitoring', label: localization.routes.emulators.components.emulatorDetailPage.monitoringTabLabel },
  { id: 'logs', label: localization.routes.emulators.components.emulatorDetailPage.logsTabLabel },
] as const;

type TabId = (typeof tabs)[number]['id'];

type TelemetryChartPoint = {
  timestamp: string;
  time: string;
  values: Record<string, number>;
} & Record<string, string | number | Record<string, number>>;

const emptyTelemetry: TelemetryPoint[] = [];

function parseTelemetryNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const normalized = value.trim().replace(',', '.');
    if (normalized.length === 0) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function formatTelemetryValue(value: unknown): string {
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (value === null || value === undefined) return '-';
  if (typeof value !== 'number' || !Number.isFinite(value)) return String(value);
  return Number(value.toFixed(2)).toLocaleString('ru-RU');
}

export function EmulatorDetailPage() {
  const { id } = useParams({ from: '/emulators/$id' });
  const emulators = useUniEmuStore((s) => s.emulators);
  const tagsByEmulator = useUniEmuStore((s) => s.tagsByEmulator);
  const allEvents = useUniEmuStore((s) => s.events);
  const toggleStatus = useUniEmuStore((s) => s.toggleStatus);
  const downloadDispatcherTemplate = useUniEmuStore((s) => s.downloadDispatcherTemplate);
  const liveTelemetry = useUniEmuStore((s) => s.telemetryByEmulator[id] ?? emptyTelemetry);
  const loadEmulatorDetails = useUniEmuStore((s) => s.loadEmulatorDetails);
  const subscribeRealtimeEmulator = useUniEmuStore((s) => s.subscribeRealtimeEmulator);
  const unsubscribeRealtimeEmulator = useUniEmuStore((s) => s.unsubscribeRealtimeEmulator);
  const cncPrograms = useUniEmuStore((s) => s.cncPrograms);

  const emulator = useMemo(() => emulators.find((e) => e.id === id), [emulators, id]);
  const tags = useMemo(() => tagsByEmulator[id] ?? [], [tagsByEmulator, id]);
  const events = useMemo(() => allEvents.filter((ev) => ev.emulatorId === id), [allEvents, id]);
  const [tab, setTab] = useState<TabId>('overview');
  const [addTagOpen, setAddTagOpen] = useState(false);
  const [editingTag, setEditingTag] = useState<EmulatorTag | null>(null);
  const [editConfigOpen, setEditConfigOpen] = useState(false);
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  const [openPackets, setOpenPackets] = useState<Record<number, boolean>>({});
  const [telemetryPaused, setTelemetryPaused] = useState(false);
  const [pausedTelemetrySnapshot, setPausedTelemetrySnapshot] = useState<TelemetryPoint[]>([]);
  const [telemetryYViewport, setTelemetryYViewport] = useState<TelemetryYViewport>({
    zoom: 1,
    offset: 0,
  });
  const [hiddenTelemetryTagNames, setHiddenTelemetryTagNames] = useState<Set<string>>(
    () => new Set()
  );
  const telemetryChartRef = useRef<HTMLDivElement | null>(null);
  const telemetryDragStartRef = useRef<{
    pointerId: number;
    y: number;
    viewport: TelemetryYViewport;
  } | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [templateDownloading, setTemplateDownloading] = useState(false);
  const [programPreviewPickerOpenId, setProgramPreviewPickerOpenId] = useState<string | null>(null);
  const deleteTag = useUniEmuStore((s) => s.deleteTag);
  const updateTag = useUniEmuStore((s) => s.updateTag);
  const deleteEmulator = useUniEmuStore((s) => s.deleteEmulator);
  const packetRetention = useUniEmuStore((s) => s.packetRetention);
  const navigate = useNavigate();
  const router = useRouter();

  useEffect(() => {
    void loadEmulatorDetails(id);
  }, [id, loadEmulatorDetails]);

  useEffect(() => {
    void subscribeRealtimeEmulator(id);
    return () => {
      void unsubscribeRealtimeEmulator(id);
    };
  }, [id, subscribeRealtimeEmulator, unsubscribeRealtimeEmulator]);

  const handleDelete = async () => {
    await deleteEmulator(id);
    setDeleteOpen(false);
    if (router.history.canGoBack()) {
      router.history.back();
      return;
    }

    void navigate({ to: '/', replace: true });
  };

  const handleTelemetryPauseToggle = () => {
    if (!telemetryPaused) {
      setPausedTelemetrySnapshot(liveTelemetry);
    }
    setTelemetryPaused((paused) => !paused);
  };

  const handleDownloadDispatcherTemplate = async () => {
    setTemplateDownloading(true);
    try {
      await downloadDispatcherTemplate(emulator.id);
    } finally {
      setTemplateDownloading(false);
    }
  };

  const visibleTelemetry = telemetryPaused ? pausedTelemetrySnapshot : liveTelemetry;
  const telemetryPoints = useMemo(() => visibleTelemetry.slice(-60), [visibleTelemetry]);
  const enabledTagsForDispatcher = useMemo(() => tags.filter((t) => t.enabled !== false), [tags]);
  const numericTelemetryTags = useMemo(
    () => enabledTagsForDispatcher.filter((t) => t.type === 'int' || t.type === 'double'),
    [enabledTagsForDispatcher]
  );
  const visibleNumericTelemetryTags = useMemo(
    () => numericTelemetryTags.filter((t) => !hiddenTelemetryTagNames.has(t.name)),
    [hiddenTelemetryTagNames, numericTelemetryTags]
  );
  const visibleCncPrograms = useMemo(
    () => [...cncPrograms
      .filter((program) =>
        program.scope === 'shared' ||
        (program.scope === 'emulator' && program.emulatorId === id))]
      .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' })),
    [cncPrograms, id]
  );
  const sharedCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'shared'),
    [visibleCncPrograms]
  );
  const emulatorCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'emulator'),
    [visibleCncPrograms]
  );

  useEffect(() => {
    const numericTagNames = new Set(numericTelemetryTags.map((t) => t.name));
    setHiddenTelemetryTagNames((current) => {
      const next = new Set([...current].filter((name) => numericTagNames.has(name)));
      return next.size === current.size ? current : next;
    });
  }, [numericTelemetryTags]);

  const telemetry = useMemo<TelemetryChartPoint[]>(() => {
    return telemetryPoints.map((p) => {
      const values = Object.fromEntries(
        visibleNumericTelemetryTags.flatMap((t) => {
          const value = parseTelemetryNumber(p.values?.[t.name]);
          return value === null ? [] : [[t.name, value]];
        })
      );

      return {
        timestamp: p.timestamp,
        time: new Date(p.timestamp).toLocaleTimeString('ru-RU', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        }),
        values,
        ...values,
      };
    });
  }, [telemetryPoints, visibleNumericTelemetryTags]);

  const telemetryKeys = useMemo(() => {
    const keys = new Set<string>();
    telemetry.forEach((p) => {
      Object.entries(p.values ?? {}).forEach(([key, value]) => {
        if (Number.isFinite(value)) keys.add(key);
      });
    });
    return [...keys];
  }, [telemetry]);
  const telemetryYBaseRange = useMemo(() => getTelemetryYBaseRange(telemetry), [telemetry]);
  const telemetryYDomain = useMemo(
    () => getTelemetryYDomain(telemetryYBaseRange, telemetryYViewport),
    [telemetryYBaseRange, telemetryYViewport]
  );

  useEffect(() => {
    setTelemetryYViewport((current) => {
      if (!telemetryYBaseRange) return { zoom: 1, offset: 0 };
      return panTelemetryYViewport(current, telemetryYBaseRange, 0);
    });
  }, [telemetryYBaseRange]);

  const toggleTelemetryTagVisibility = (tagName: string) => {
    setHiddenTelemetryTagNames((current) => {
      const next = new Set(current);
      if (next.has(tagName)) {
        next.delete(tagName);
      } else {
        next.add(tagName);
      }
      return next;
    });
  };

  const handleTelemetryChartWheel = (event: ReactWheelEvent<HTMLDivElement>) => {
    if (!telemetryYBaseRange) return;

    event.preventDefault();
    setTelemetryYViewport((current) =>
      panTelemetryYViewport(
        zoomTelemetryYViewport(current, event.deltaY),
        telemetryYBaseRange,
        0
      )
    );
  };

  const handleTelemetryChartPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!telemetryYBaseRange || telemetryYViewport.zoom <= 1) return;

    telemetryDragStartRef.current = {
      pointerId: event.pointerId,
      y: event.clientY,
      viewport: telemetryYViewport,
    };
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const handleTelemetryChartPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const dragStart = telemetryDragStartRef.current;
    if (!dragStart || dragStart.pointerId !== event.pointerId || !telemetryYBaseRange) return;

    const chartHeight = telemetryChartRef.current?.getBoundingClientRect().height ?? 0;
    const startDomain = getTelemetryYDomain(telemetryYBaseRange, dragStart.viewport);
    if (chartHeight <= 0 || !startDomain) return;

    const valueDelta = ((event.clientY - dragStart.y) / chartHeight) * (startDomain[1] - startDomain[0]);
    setTelemetryYViewport(panTelemetryYViewport(dragStart.viewport, telemetryYBaseRange, valueDelta));
  };

  const handleTelemetryChartPointerEnd = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (telemetryDragStartRef.current?.pointerId !== event.pointerId) return;

    telemetryDragStartRef.current = null;
    event.currentTarget.releasePointerCapture(event.pointerId);
  };

  // Build packet history (newest first), bounded by retention setting
  const packets = useMemo(() => {
    const arr = telemetryPoints.map((p, i) => ({
      idx: i,
      timestamp: p.timestamp,
      values: enabledTagsForDispatcher.reduce<Record<string, string>>((acc, t) => {
        acc[t.name] = formatTelemetryValue(p.values?.[t.name]);
        return acc;
      }, {}),
    }));
    return arr.slice().reverse().slice(0, packetRetention);
  }, [telemetryPoints, enabledTagsForDispatcher, packetRetention]);

  const activeIdx =
    telemetryPoints.length === 0
      ? -1
      : Math.min(hoverIdx ?? telemetryPoints.length - 1, telemetryPoints.length - 1);
  const activePoint = telemetryPoints[activeIdx];

  const isProgramNameTag = (t: EmulatorTag) =>
    t.source === 'static' &&
    t.type === 'string' &&
    (t.specialParameter === 'PrgName' || t.specialParameter === 'Subprogram');

  const renderProgramOption = (t: EmulatorTag, program: CncProgram) => (
    <CommandItem
      key={program.id}
      value={`${program.scope}:${program.name}:${program.description}`}
      onSelect={() => {
        void updateTag(id, t.id, { ...t, preview: program.name });
        setProgramPreviewPickerOpenId(null);
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
          t.preview === program.name ? 'opacity-100' : 'opacity-0'
        )}
      />
    </CommandItem>
  );

  const renderProgramPreviewPicker = (t: EmulatorTag) => {
    const selectedCncProgram = visibleCncPrograms.find(
      (program) => program.name.localeCompare(t.preview, undefined, { sensitivity: 'accent' }) === 0
    );

    return (
      <Popover
        open={programPreviewPickerOpenId === t.id}
        onOpenChange={(open) => setProgramPreviewPickerOpenId(open ? t.id : null)}
      >
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            className="h-7 w-48 max-w-[18rem] justify-between gap-2 px-2 py-1 font-mono text-xs"
          >
            <span className={cn('truncate', !t.preview && 'font-sans text-muted-foreground')}>
              {t.preview || localization.routes.emulators.components.addTagDrawer.programPickerSelectPlaceholder}
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
              {t.preview && !selectedCncProgram && (
                <CommandGroup
                  heading={localization.routes.emulators.components.addTagDrawer.programPickerCurrentGroup}
                >
                  <CommandItem
                    value={`current:${t.preview}`}
                    onSelect={() => setProgramPreviewPickerOpenId(null)}
                    className="items-start gap-2 py-2"
                  >
                    <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <div className="truncate font-mono text-xs">{t.preview}</div>
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
                  {sharedCncPrograms.map((program) => renderProgramOption(t, program))}
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
                  {emulatorCncPrograms.map((program) => renderProgramOption(t, program))}
                </CommandGroup>
              )}
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    );
  };

  if (!emulator) return null;

  return (
    <div className="space-y-4 p-6">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="space-y-2">
          <Link
            to="/emulators"
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="h-3 w-3" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.backToListLabel}
          </Link>
          <div className="flex items-center gap-3">
            <h1 className="font-mono text-2xl font-semibold">{emulator.name}</h1>
            <StatusBadge status={emulator.status} />
          </div>
        </div>
        <div className="flex gap-2">
          {/* <Button variant="outline" size="sm" className="gap-2">
            <RotateCw className="h-3.5 w-3.5" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.runOnceButtonLabel}
          </Button> */}
          <Button
            size="sm"
            variant="outline"
            className="gap-2"
            disabled={templateDownloading}
            onClick={() => void handleDownloadDispatcherTemplate()}
            title={localization.routes.emulators.components.emulatorDetailPage.downloadDispatcherTemplate}
          >
            <Download className="h-3.5 w-3.5" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.downloadDispatcherTemplate}
          </Button>
          <Button
            size="sm"
            variant={emulator.status === 'Running' ? 'destructive' : 'default'}
            className="gap-2"
            onClick={() => void toggleStatus(emulator.id)}
          >
            {emulator.status === 'Running' ? (
              <>
                <StopCircle className="h-3.5 w-3.5" />{' '}
                {localization.routes.emulators.components.emulatorDetailPage.stopButtonLabel}
              </>
            ) : (
              <>
                <PlayCircle className="h-3.5 w-3.5" />{' '}
                {localization.routes.emulators.components.emulatorDetailPage.startButtonLabel}
              </>
            )}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-2 border-signal-offline/40 text-signal-offline hover:bg-signal-offline/10 hover:text-signal-offline"
            onClick={() => setDeleteOpen(true)}
            title={localization.routes.emulators.components.emulatorDetailPage.deleteEmulatorButtonLabel}
          >
            <Trash2 className="h-3.5 w-3.5" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.deleteButtonLabel}
          </Button>
        </div>
      </div>

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogSubjectLabel}
              <span className="font-mono text-foreground">{emulator.name}</span>{' '}
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogDescriptionSuffix}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.emulatorDetailPage.cancelButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={() => void handleDelete()}
              className="bg-signal-offline text-white hover:bg-signal-offline/90"
            >
              {localization.routes.emulators.components.emulatorDetailPage.confirmDeleteButtonLabel}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <div className="flex gap-1 border-b border-border">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`relative px-4 py-2 text-sm font-medium transition-colors ${
              tab === t.id ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {t.label}
            {tab === t.id && (
              <span className="absolute -bottom-px left-0 h-0.5 w-full bg-primary" />
            )}
          </button>
        ))}
      </div>

      {tab === 'overview' && (
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
              onClick={() => setEditConfigOpen(true)}
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
      )}

      {tab === 'tags' &&
        (() => {
          const enabledTags = tags.filter((t) => t.enabled !== false);
          const disabledTags = tags.filter((t) => t.enabled === false);

          const renderRow = (t: (typeof tags)[number]) => (
            <tr
              key={t.id}
              className="border-b border-border/60 transition-colors hover:bg-muted/20"
            >
              <td className="px-4 py-3 font-mono">{t.name}</td>
              <td className="px-4 py-3 font-mono text-xs text-muted-foreground">
                {t.key === 'Custom' ? '-' : t.key}
              </td>
              <td className="px-4 py-3">
                <span className="rounded bg-muted px-2 py-0.5 font-mono text-[10px] uppercase">
                  {getTagTypeLabel(t.type)}
                </span>
              </td>
              <td className="px-4 py-3 text-xs text-muted-foreground">
                {getTagSourceLabel(t.source)}
              </td>
              <td className="px-4 py-3 text-xs text-muted-foreground">
                {formatTrigger(t.trigger, t.source)}
              </td>
              <td className="px-4 py-3 font-mono text-[11px] text-muted-foreground">
                {t.source === 'scenario' && t.scenario
                  ? localization.routes.emulators.components.emulatorDetailPage.scenarioSummary(
                      t.scenario.segments.length,
                      formatDuration(totalDuration(t.scenario)),
                      getContinueOnFormulaEndLabel(t.scenario.continueOnFormulaEnd ?? 'Repeat')
                    )
                  : getCalcTypeLabel(t.calc?.type ?? 'None')}
              </td>
              <td className="px-4 py-3 font-mono text-xs text-primary">
                {t.source === 'scenario' && t.scenario ? (
                  <ScenarioSparkline scenario={t.scenario} />
                ) : t.source === 'static' ? (
                  t.type === 'bool' ? (
                    <Switch
                      checked={t.preview === 'true'}
                      onCheckedChange={(v) =>
                        void updateTag(id, t.id, { ...t, preview: v ? 'true' : 'false' })
                      }
                    />
                  ) : isProgramNameTag(t) ? (
                    renderProgramPreviewPicker(t)
                  ) : (
                    <Input
                      type="text"
                      inputMode={
                        t.type === 'int' ? 'numeric' : t.type === 'double' ? 'decimal' : undefined
                      }
                      spellCheck={t.type === 'string'}
                      defaultValue={t.preview}
                      onBlur={(e) => {
                        const v = e.currentTarget.value;
                        if (v !== t.preview) void updateTag(id, t.id, { ...t, preview: v });
                      }}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') (e.currentTarget as HTMLInputElement).blur();
                      }}
                      className="h-7 w-48 max-w-[18rem] px-2 py-1 font-mono text-xs"
                    />
                  )
                ) : (
                  t.preview
                )}
              </td>
              <td className="px-4 py-3 text-right">
                <div className="flex items-center justify-end gap-2">
                  <Switch
                    checked={t.enabled !== false}
                    onCheckedChange={(v) => void updateTag(id, t.id, { ...t, enabled: v })}
                    title={localization.routes.emulators.components.emulatorDetailPage.sendTagTitle}
                  />
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7 text-muted-foreground hover:text-foreground"
                    onClick={() => {
                      setEditingTag(t);
                      setAddTagOpen(true);
                    }}
                    title={localization.routes.emulators.components.emulatorDetailPage.editTagTitle}
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7 text-muted-foreground hover:text-signal-offline"
                    onClick={() => void deleteTag(id, t.id)}
                    title={localization.routes.emulators.components.emulatorDetailPage.deleteTagTitle}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </td>
            </tr>
          );

          const renderTable = (rows: typeof tags, emptyText: string, muted = false) => (
            <table className={`w-full text-sm ${muted ? 'opacity-70' : ''}`}>
              <thead className="border-b border-border bg-muted/30 text-left text-[11px] uppercase tracking-wider text-muted-foreground">
                <tr>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.nameColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.keyColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.typeColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.sourceColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.triggerColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.calcColumnLabel}
                  </th>
                  <th className="px-4 py-2 font-medium">
                    {localization.routes.emulators.components.emulatorDetailPage.previewColumnLabel}
                  </th>
                  <th className="px-4 py-2"></th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                      {emptyText}
                    </td>
                  </tr>
                )}
                {rows.map(renderRow)}
              </tbody>
            </table>
          );

          return (
            <div className="space-y-4">
              <div className="rounded-lg border border-border bg-card">
                <div className="flex items-center justify-between border-b border-border p-4">
                  <div>
                    <h3 className="font-semibold">
                      {localization.routes.emulators.components.emulatorDetailPage.enabledTagsSectionTitle}
                    </h3>
                    <p className="text-xs text-muted-foreground">
                      {localization.routes.emulators.components.emulatorDetailPage.enabledTagsSectionDescription}{' '}
                      {localization.routes.emulators.components.emulatorDetailPage.tagsCountLabel(enabledTags.length)}
                    </p>
                  </div>
                  <Button
                    size="sm"
                    onClick={() => {
                      setEditingTag(null);
                      setAddTagOpen(true);
                    }}
                    title={localization.routes.emulators.components.emulatorDetailPage.addTagButtonLabel}
                  >
                    <Plus className="h-3.5 w-3.5" />{' '}
                    {localization.routes.emulators.components.emulatorDetailPage.addTagButtonLabel}
                  </Button>
                </div>
                {renderTable(
                  enabledTags,
                  localization.routes.emulators.components.emulatorDetailPage.emptyEnabledTagsMessage
                )}
              </div>

              {disabledTags.length > 0 && (
                <div className="rounded-lg border border-dashed border-border bg-card/50">
                  <div className="border-b border-border/60 p-4">
                    <h3 className="font-semibold text-muted-foreground">
                      {localization.routes.emulators.components.emulatorDetailPage.disabledTagsSectionTitle}
                    </h3>
                    <p className="text-xs text-muted-foreground">
                      {localization.routes.emulators.components.emulatorDetailPage.disabledTagsSectionDescription}{' '}
                      {localization.routes.emulators.components.emulatorDetailPage.tagsCountLabel(disabledTags.length)}
                    </p>
                  </div>
                  {renderTable(
                    disabledTags,
                    localization.routes.emulators.components.emulatorDetailPage.emptyDisabledTagsMessage,
                    true
                  )}
                </div>
              )}
            </div>
          );
        })()}

      <AddTagDrawer
        emulatorId={id}
        open={addTagOpen}
        onOpenChange={(o) => {
          setAddTagOpen(o);
          if (!o) setEditingTag(null);
        }}
        tag={editingTag}
      />

      <EditEmulatorDrawer
        emulator={emulator}
        open={editConfigOpen}
        onOpenChange={setEditConfigOpen}
      />

      {tab === 'monitoring' && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
            <div className="rounded-lg border border-border bg-card p-4 xl:col-span-2">
              <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
                <h3 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.telemetryTimeSeriesTitle}
                </h3>
                <div className="flex flex-wrap items-center justify-end gap-2">
                  <p className="font-mono text-[11px] text-muted-foreground">
                    {hoverIdx !== null
                      ? `t = ${telemetry[hoverIdx]?.time ?? '-'}`
                      : localization.routes.emulators.components.emulatorDetailPage.chartHoverHint}
                  </p>
                  <Button
                    size="sm"
                    variant={telemetryPaused ? 'default' : 'outline'}
                    className="h-7 gap-2 px-2.5"
                    onClick={handleTelemetryPauseToggle}
                  >
                    {telemetryPaused ? (
                      <>
                        <PlayCircle className="h-3.5 w-3.5" />{' '}
                        {localization.routes.emulators.components.emulatorDetailPage.resume}
                      </>
                    ) : (
                      <>
                        <PauseCircle className="h-3.5 w-3.5" />{' '}
                        {localization.routes.emulators.components.emulatorDetailPage.pause}
                      </>
                    )}
                  </Button>
                </div>
              </div>
              {numericTelemetryTags.length > 0 && (
                <div className="mb-3 flex flex-wrap gap-2">
                  {numericTelemetryTags.map((t) => (
                    <label
                      key={t.id}
                      className="flex h-7 items-center gap-2 rounded-md border border-border bg-background/40 px-2 text-xs"
                    >
                      <Checkbox
                        checked={!hiddenTelemetryTagNames.has(t.name)}
                        onCheckedChange={() => toggleTelemetryTagVisibility(t.name)}
                        className="h-3.5 w-3.5"
                      />
                      <span className="max-w-36 truncate font-mono">{t.name}</span>
                    </label>
                  ))}
                </div>
              )}
              <div
                ref={telemetryChartRef}
                className={cn(
                  'h-[320px] w-full touch-none select-none',
                  telemetryYViewport.zoom > 1 ? 'cursor-grab active:cursor-grabbing' : 'cursor-zoom-in'
                )}
                onWheel={handleTelemetryChartWheel}
                onPointerDown={handleTelemetryChartPointerDown}
                onPointerMove={handleTelemetryChartPointerMove}
                onPointerUp={handleTelemetryChartPointerEnd}
                onPointerCancel={handleTelemetryChartPointerEnd}
              >
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart
                    data={telemetry}
                    onMouseMove={(s) => {
                      const idx = Number(s?.activeTooltipIndex ?? s?.activeIndex);
                      if (Number.isFinite(idx) && idx >= 0) setHoverIdx(idx);
                    }}
                    onMouseLeave={() => setHoverIdx(null)}
                  >
                    <CartesianGrid stroke="oklch(0.32 0.02 240)" strokeDasharray="3 3" />
                    <XAxis
                      dataKey="time"
                      stroke="oklch(0.68 0.025 235)"
                      fontSize={10}
                      tickLine={false}
                    />
                    <YAxis
                      domain={telemetryYDomain}
                      allowDataOverflow
                      stroke="oklch(0.68 0.025 235)"
                      fontSize={10}
                      tickLine={false}
                    />
                    <Tooltip
                      contentStyle={{
                        background: 'var(--popover)',
                        border: '1px solid var(--border)',
                        borderRadius: 6,
                        color: 'var(--popover-foreground)',
                        fontSize: 12,
                      }}
                      labelStyle={{ color: 'var(--popover-foreground)' }}
                      itemStyle={{ color: 'var(--popover-foreground)' }}
                    />
                    {telemetryKeys.map((key) => {
                      const tagIndex = numericTelemetryTags.findIndex((t) => t.name === key);
                      return (
                        <Line
                          key={key}
                          type="linear"
                          dataKey={key}
                          stroke={TELEMETRY_LINE_COLORS[tagIndex % TELEMETRY_LINE_COLORS.length]}
                          strokeWidth={2}
                          dot={false}
                          isAnimationActive={false}
                        />
                      );
                    })}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="rounded-lg border border-border bg-card">
              <div className="border-b border-border p-4">
                <h3 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.tagValuesTitle}
                </h3>
                <p className="mt-1 font-mono text-[11px] text-muted-foreground">
                  {hoverIdx !== null
                    ? localization.routes.emulators.components.emulatorDetailPage.cursorSnapshotLabel
                    : localization.routes.emulators.components.emulatorDetailPage.latestSnapshotLabel}
                </p>
              </div>
              <div className="max-h-[320px] overflow-auto">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 border-b border-border bg-card text-left text-[10px] uppercase tracking-wider text-muted-foreground">
                    <tr>
                      <th className="px-3 py-2 font-medium">
                        {localization.routes.emulators.components.emulatorDetailPage.tagColumnLabel}
                      </th>
                      <th className="px-3 py-2 font-medium">
                        {localization.routes.emulators.components.emulatorDetailPage.valueTypeColumnLabel}
                      </th>
                      <th className="px-3 py-2 text-right font-medium">
                        {localization.routes.emulators.components.emulatorDetailPage.valueColumnLabel}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {tags.length === 0 && (
                      <tr>
                        <td
                          colSpan={3}
                          className="px-3 py-6 text-center text-xs text-muted-foreground"
                        >
                          {localization.routes.emulators.components.emulatorDetailPage.emptyTagsMessage}
                        </td>
                      </tr>
                    )}
                    {tags.map((t) => (
                      <tr key={t.id} className="border-b border-border/40">
                        <td className="px-3 py-2 font-mono text-xs">{t.name}</td>
                        <td className="px-3 py-2">
                          <span className="rounded bg-muted px-1.5 py-0.5 font-mono text-[10px] uppercase">
                            {t.type}
                          </span>
                        </td>
                        <td className="px-3 py-2 text-right font-mono text-xs text-primary">
                          {formatTelemetryValue(activePoint?.values?.[t.name])}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          {/* Сценарии тегов */}
          {SHOW_TAG_SCENARIO_PREVIEWS && tags.some((t) => t.source === 'scenario' && t.scenario) && (
            <div className="rounded-lg border border-border bg-card">
              <div className="border-b border-border p-4">
                <h3 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.tagScenariosTitle}
                </h3>
                <p className="mt-1 text-xs text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.tagScenariosDescription}
                </p>
              </div>
              <div className="grid grid-cols-1 gap-4 p-4 xl:grid-cols-2">
                {tags
                  .filter((t) => t.source === 'scenario' && t.scenario)
                  .map((t) => {
                    const total = totalDuration(t.scenario!);
                    const stepSec = 5;
                    const endMode = t.scenario!.continueOnFormulaEnd ?? 'Repeat';
                    const tCursor =
                      endMode === 'Repeat'
                        ? (activeIdx * stepSec) % Math.max(total, 1)
                        : Math.min(activeIdx * stepSec, total);
                    return (
                      <div
                        key={t.id}
                        className="rounded-md border border-border bg-background/40 p-3"
                      >
                        <div className="mb-2 flex items-center justify-between">
                          <div>
                            <p className="font-mono text-sm">{t.name}</p>
                            <p className="text-[11px] text-muted-foreground">
                              {t.scenario!.segments.length}{' '}
                              {localization.routes.emulators.components.emulatorDetailPage.segmentsSumLabel}
                              {formatDuration(total)}
                              {` · ${endMode}`}
                            </p>
                          </div>
                          <span className="font-mono text-xs text-primary">
                            {formatTelemetryValue(activePoint?.values?.[t.name])}
                          </span>
                        </div>
                        <ScenarioPreviewChart
                          scenario={t.scenario!}
                          cursorSec={tCursor}
                          height={140}
                        />
                      </div>
                    );
                  })}
              </div>
            </div>
          )}
          <div className="rounded-lg border border-border bg-card">
            <div className="flex items-center justify-between border-b border-border p-4">
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.packetHistoryTitle}
                </h3>
                <p className="mt-1 text-xs text-muted-foreground">
                  {localization.routes.emulators.components.emulatorDetailPage.packetHistorySummary(
                    packetRetention,
                    packets.length
                  )}
                </p>
              </div>
            </div>
            <div className="divide-y divide-border/40">
              {packets.map((pkt, listIdx) => {
                const isOpen =
                  listIdx === 0 ? openPackets[pkt.idx] !== false : !!openPackets[pkt.idx];
                const json = JSON.stringify(
                  {
                    emulatorId: emulator.id,
                    timestamp: pkt.timestamp,
                    tags: enabledTagsForDispatcher.map((t) => ({
                      name: t.name,
                      value: pkt.values[t.name],
                      type: t.type,
                    })),
                  },
                  null,
                  2
                );
                return (
                  <Collapsible
                    key={pkt.idx}
                    open={isOpen}
                    onOpenChange={(o) => setOpenPackets((s) => ({ ...s, [pkt.idx]: o }))}
                  >
                    <CollapsibleTrigger className="flex w-full items-center justify-between gap-3 px-4 py-2.5 text-left transition-colors hover:bg-muted/30">
                      <div className="flex items-center gap-2">
                        {isOpen ? (
                          <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                        ) : (
                          <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                        )}
                        <span className="font-mono text-xs">
                          {new Date(pkt.timestamp).toLocaleTimeString('ru-RU')}
                        </span>
                        {listIdx === 0 && (
                          <span className="rounded bg-primary/15 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wider text-primary">
                            {localization.routes.emulators.components.emulatorDetailPage.latestPacketLabel}
                          </span>
                        )}
                      </div>
                      <span className="font-mono text-[11px] text-muted-foreground">
                        {localization.routes.emulators.components.emulatorDetailPage.tagsCountLabel(enabledTagsForDispatcher.length)}
                      </span>
                    </CollapsibleTrigger>
                    <CollapsibleContent>
                      <pre className="overflow-x-auto bg-background p-4 font-mono text-xs leading-relaxed text-foreground">
                        {json}
                      </pre>
                    </CollapsibleContent>
                  </Collapsible>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {tab === 'logs' && (
        <div className="rounded-lg border border-border bg-card">
          <div className="border-b border-border p-4">
            <h3 className="font-semibold">
              {localization.routes.emulators.components.emulatorDetailPage.eventsLogTitle}
            </h3>
            <p className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.emulatorDetailPage.eventsCountLabel(events.length)}
            </p>
          </div>
          <div className="divide-y divide-border/40">
            {events.length === 0 && (
              <div className="p-8 text-center text-sm text-muted-foreground">
                {localization.routes.emulators.components.emulatorDetailPage.emptyEventsMessage}
              </div>
            )}
            {events.map((ev) => (
              <div key={ev.id} className="flex gap-3 px-4 py-2.5 font-mono text-xs">
                <span className="w-32 shrink-0 text-muted-foreground">
                  {new Date(ev.timestamp).toLocaleString('ru-RU')}
                </span>
                <span
                  className={`w-16 shrink-0 uppercase ${
                    ev.level === 'error'
                      ? 'text-signal-offline'
                      : ev.level === 'warn'
                        ? 'text-signal-warning'
                        : ev.level === 'success'
                          ? 'text-signal-online'
                          : 'text-signal-info'
                  }`}
                >
                  {ev.level}
                </span>
                <span className="text-foreground">{ev.message}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
