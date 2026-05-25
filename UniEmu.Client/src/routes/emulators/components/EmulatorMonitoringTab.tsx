import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { PauseCircle, PlayCircle, ChevronDown, ChevronRight } from 'lucide-react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { TELEMETRY_CHART_VISIBLE_PACKET_COUNT, TELEMETRY_LINE_COLORS } from '@/lib/constants';
import { SHOW_TAG_SCENARIO_PREVIEWS } from '@/lib/feature-flags';
import { localization } from '@/localization';
import { useUniEmuStore } from '@/store/uniemu-store';
import type { EmulatorTag, TelemetryPoint } from '@/types/uniemu';
import { formatDuration, totalDuration } from './tag-scenario/scenarioMath';
import { ScenarioPreviewChart } from './tag-scenario/ScenarioPreviewChart';
import {
  areMonitoringTagsEqual,
  buildPacketHistory,
  buildTelemetryChartPoints,
  emptyTelemetry,
  formatTelemetryValue,
  getTelemetryKeys,
  readHiddenTelemetryTagNames,
  writeHiddenTelemetryTagNames,
} from './emulator-detail/telemetry';

type EmulatorMonitoringTabProps = {
  emulatorId: string;
  protocolId: number;
  tags: EmulatorTag[];
  packetRetention: number;
};

export const EmulatorMonitoringTab = memo(function EmulatorMonitoringTab({
  emulatorId,
  protocolId,
  tags,
  packetRetention,
}: EmulatorMonitoringTabProps) {
  const id = emulatorId;
  const liveTelemetry = useUniEmuStore((s) => s.telemetryByEmulator[id] ?? emptyTelemetry);
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  const [openPackets, setOpenPackets] = useState<Record<number, boolean>>({});
  const [telemetryPaused, setTelemetryPaused] = useState(false);
  const [pausedTelemetrySnapshot, setPausedTelemetrySnapshot] = useState<TelemetryPoint[]>([]);
  const [hiddenTelemetryTagNames, setHiddenTelemetryTagNames] = useState<Set<string>>(
    () => readHiddenTelemetryTagNames(emulatorId)
  );

  const handleTelemetryPauseToggle = useCallback(() => {
    if (!telemetryPaused) setPausedTelemetrySnapshot(liveTelemetry);
    setTelemetryPaused((paused) => !paused);
  }, [liveTelemetry, telemetryPaused]);

  const visibleTelemetry = telemetryPaused ? pausedTelemetrySnapshot : liveTelemetry;
  const telemetryPoints = useMemo(
    () => visibleTelemetry.slice(-TELEMETRY_CHART_VISIBLE_PACKET_COUNT),
    [visibleTelemetry]
  );
  const enabledTagsForDispatcher = useMemo(() => tags.filter((t) => t.enabled !== false), [tags]);
  const numericTelemetryTags = useMemo(
    () => enabledTagsForDispatcher.filter((t) => t.type === 'int' || t.type === 'double'),
    [enabledTagsForDispatcher]
  );
  const visibleNumericTelemetryTags = useMemo(
    () => numericTelemetryTags.filter((t) => !hiddenTelemetryTagNames.has(t.name)),
    [hiddenTelemetryTagNames, numericTelemetryTags]
  );

  useEffect(() => {
    const numericTagNames = new Set(numericTelemetryTags.map((t) => t.name));
    const next = new Set(
      [...readHiddenTelemetryTagNames(emulatorId)].filter((name) => numericTagNames.has(name))
    );
    writeHiddenTelemetryTagNames(emulatorId, next);
    setHiddenTelemetryTagNames(next);
  }, [emulatorId, numericTelemetryTags]);

  const telemetry = useMemo(
    () => buildTelemetryChartPoints(telemetryPoints, visibleNumericTelemetryTags),
    [telemetryPoints, visibleNumericTelemetryTags]
  );
  const telemetryKeys = useMemo(() => getTelemetryKeys(telemetry), [telemetry]);

  const toggleTelemetryTagVisibility = useCallback((tagName: string) => {
    setHiddenTelemetryTagNames((current) => {
      const next = new Set(current);
      if (next.has(tagName)) {
        next.delete(tagName);
      } else {
        next.add(tagName);
      }
      writeHiddenTelemetryTagNames(emulatorId, next);
      return next;
    });
  }, [emulatorId]);

  const packets = useMemo(
    () => buildPacketHistory(telemetryPoints, enabledTagsForDispatcher, packetRetention),
    [telemetryPoints, enabledTagsForDispatcher, packetRetention]
  );

  const activeIdx =
    telemetryPoints.length === 0
      ? -1
      : Math.min(hoverIdx ?? telemetryPoints.length - 1, telemetryPoints.length - 1);
  const activePoint = telemetryPoints[activeIdx];

  const handleChartMouseMove = useCallback((state: unknown) => {
    const chartState = state as { activeTooltipIndex?: unknown; activeIndex?: unknown };
    const idx = Number(chartState?.activeTooltipIndex ?? chartState?.activeIndex);
    if (Number.isFinite(idx) && idx >= 0) {
      setHoverIdx((current) => (current === idx ? current : idx));
    }
  }, []);

  const handleChartMouseLeave = useCallback(() => {
    setHoverIdx((current) => (current === null ? current : null));
  }, []);

  return (
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
          <div className="h-[320px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart
                data={telemetry}
                onMouseMove={handleChartMouseMove}
                onMouseLeave={handleChartMouseLeave}
              >
                <CartesianGrid stroke="oklch(0.32 0.02 240)" strokeDasharray="3 3" />
                <XAxis
                  dataKey="time"
                  stroke="oklch(0.68 0.025 235)"
                  fontSize={10}
                  tickLine={false}
                />
                <YAxis stroke="oklch(0.68 0.025 235)" fontSize={10} tickLine={false} />
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

        <div className="relative rounded-lg border border-border bg-card xl:min-h-0 xl:overflow-hidden">
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
          <div className="max-h-[320px] overflow-auto xl:absolute xl:inset-x-0 xl:bottom-0 xl:top-[74px] xl:max-h-none">
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
                      tagType={t.type}
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
                MachineIntegrationId: protocolId,
                ListValues: enabledTagsForDispatcher.map((t) => ({
                  Key: t.key,
                  Value: pkt.values[t.name],
                })),
              },
              null,
              2
            );
            return (
              <Collapsible
                key={pkt.timestamp}
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
  );
}, areEmulatorMonitoringTabPropsEqual);

function areEmulatorMonitoringTabPropsEqual(
  prev: EmulatorMonitoringTabProps,
  next: EmulatorMonitoringTabProps,
): boolean {
  return prev.emulatorId === next.emulatorId &&
    prev.protocolId === next.protocolId &&
    prev.packetRetention === next.packetRetention &&
    areMonitoringTagsEqual(prev.tags, next.tags);
}
