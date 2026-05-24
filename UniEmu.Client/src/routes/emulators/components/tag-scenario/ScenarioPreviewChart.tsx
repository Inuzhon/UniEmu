import { useMemo } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceArea,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { TagScenarioConfig, TagType } from '@/types/uniemu';
import { sampleScenario, scenarioTextSegments, totalDuration, formatDuration } from './scenarioMath';
import { localization } from '@/localization';

interface Props {
  scenario: TagScenarioConfig;
  height?: number;
  /** Курсор по времени в секундах (для подсветки текущей позиции) */
  cursorSec?: number | null;
  showAxes?: boolean;
  samples?: number;
  /** Клик по чарту → отдаёт время в секундах */
  onPointClick?: (tSec: number) => void;
  /** Индекс сегмента, который надо мягко подсветить полупрозрачной зоной */
  highlightSegmentIdx?: number;
  tagType?: TagType;
}

const SEG_COLORS = [
  'oklch(0.78 0.16 195)',
  'oklch(0.82 0.16 80)',
  'oklch(0.78 0.18 155)',
  'oklch(0.78 0.16 320)',
  'oklch(0.82 0.16 30)',
];

export function ScenarioPreviewChart(props: Props) {
  const {
    scenario,
    height = 160,
    cursorSec = null,
    onPointClick,
    highlightSegmentIdx,
    tagType = 'double',
  } = props;

  if (tagType === 'string') {
    return (
      <ScenarioStringTimeline
        scenario={scenario}
        height={height}
        cursorSec={cursorSec}
        onPointClick={onPointClick}
        highlightSegmentIdx={highlightSegmentIdx}
      />
    );
  }

  return <ScenarioNumericLineChart {...props} />;
}

function ScenarioNumericLineChart({
  scenario,
  height = 160,
  cursorSec = null,
  showAxes = true,
  samples = 200,
  onPointClick,
  highlightSegmentIdx,
}: Props) {
  const { data, total, segBounds, highlightRange } = useMemo(() => {
    const pts = sampleScenario(scenario, samples);
    const total = totalDuration(scenario);
    const bounds: number[] = [];
    let acc = 0;
    let hlX1: number | null = null;
    let hlX2: number | null = null;
    scenario.segments.forEach((s, i) => {
      if (i === highlightSegmentIdx) {
        hlX1 = acc;
        hlX2 = acc + s.duration;
      }
      acc += s.duration;
      if (i < scenario.segments.length - 1) bounds.push(acc);
    });
    return {
      data: pts.map((p) => ({ t: p.t, value: p.value, seg: p.segmentIdx })),
      total,
      segBounds: bounds,
      highlightRange: hlX1 !== null && hlX2 !== null ? { x1: hlX1, x2: hlX2 } : null,
    };
  }, [scenario, samples, highlightSegmentIdx]);

  if (data.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-md border border-dashed border-border bg-muted/10 text-xs text-muted-foreground"
        style={{ height }}
      >
        {localization.routes.emulators.components.tagScenario.scenarioPreviewChart.emptyPreviewMessage}
      </div>
    );
  }

  return (
    <div style={{ height }} className="w-full">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart
          data={data}
          margin={{ top: 8, right: 12, bottom: showAxes ? 4 : 0, left: showAxes ? 0 : 0 }}
          onClick={(e) => {
            if (!onPointClick) return;
            const t = (e as { activeLabel?: number | string } | null)?.activeLabel;
            if (t !== undefined && t !== null) onPointClick(Number(t));
          }}
          style={onPointClick ? { cursor: 'crosshair' } : undefined}
        >
          {showAxes && <CartesianGrid stroke="oklch(0.32 0.02 240)" strokeDasharray="3 3" />}
          {showAxes && (
            <XAxis
              dataKey="t"
              type="number"
              domain={[0, total]}
              tickFormatter={(v) => formatDuration(Number(v))}
              stroke="oklch(0.68 0.025 235)"
              fontSize={10}
              tickLine={false}
            />
          )}
          {showAxes && (
            <YAxis
              stroke="oklch(0.68 0.025 235)"
              fontSize={10}
              tickLine={false}
              width={42}
              domain={['auto', 'auto']}
              padding={{ top: 8, bottom: 8 }}
            />
          )}
          {showAxes && (
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
              labelFormatter={(v) => `t = ${formatDuration(Number(v))}`}
              formatter={(v) => [Number(v).toFixed(2), 'value']}
            />
          )}
          {highlightRange && (
            <ReferenceArea
              x1={highlightRange.x1}
              x2={highlightRange.x2}
              fill="oklch(0.82 0.18 195)"
              fillOpacity={0.08}
              stroke="oklch(0.82 0.18 195)"
              strokeOpacity={0.35}
            />
          )}
          {segBounds.map((b, i) => (
            <ReferenceLine
              key={i}
              x={b}
              stroke={SEG_COLORS[(i + 1) % SEG_COLORS.length]}
              strokeDasharray="2 3"
              strokeOpacity={0.6}
            />
          ))}
          {cursorSec !== null && cursorSec !== undefined && (
            <ReferenceLine x={cursorSec} stroke="oklch(0.85 0.18 30)" strokeWidth={1.5} />
          )}
          <Line
            type="monotone"
            dataKey="value"
            stroke="oklch(0.82 0.18 195)"
            strokeWidth={2.5}
            dot={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

function segmentColor(index: number) {
  return SEG_COLORS[index % SEG_COLORS.length];
}

function ScenarioStringTimeline({
  scenario,
  height,
  cursorSec,
  onPointClick,
  highlightSegmentIdx,
}: {
  scenario: TagScenarioConfig;
  height: number;
  cursorSec: number | null;
  onPointClick?: (tSec: number) => void;
  highlightSegmentIdx?: number;
}) {
  const segments = useMemo(() => scenarioTextSegments(scenario), [scenario]);
  const total = totalDuration(scenario);
  const cursorLeft =
    cursorSec !== null && total > 0
      ? `${Math.max(0, Math.min(100, (cursorSec / total) * 100))}%`
      : null;

  if (segments.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-md border border-dashed border-border bg-muted/10 text-xs text-muted-foreground"
        style={{ height }}
      >
        {localization.routes.emulators.components.tagScenario.scenarioPreviewChart.emptyPreviewMessage}
      </div>
    );
  }

  return (
    <div
      className="flex w-full flex-col justify-center rounded-md border border-border bg-background/40 px-3 py-2"
      style={{ height }}
    >
      <div className="relative h-16 overflow-hidden rounded-md border border-border bg-muted/10">
        <div className="flex h-full">
          {segments.map((segment) => {
            const color = segmentColor(segment.segmentIdx);
            const tooltip = `${formatDuration(segment.tStart)} - ${formatDuration(segment.tEnd)} · ${
              segment.label || `#${segment.segmentIdx + 1}`
            } · ${segment.value || '-'}`;
            return (
              <button
                key={segment.id}
                type="button"
                title={tooltip}
                onClick={() => onPointClick?.(segment.tStart + segment.duration / 2)}
                className="group flex min-w-0 flex-col justify-center border-r border-background/70 px-2 text-left last:border-r-0"
                style={{
                  flexBasis: `${(segment.duration / Math.max(total, 1)) * 100}%`,
                  backgroundColor: color,
                  opacity: highlightSegmentIdx === segment.segmentIdx ? 0.88 : 0.26,
                  cursor: onPointClick ? 'pointer' : undefined,
                }}
              >
                <span className="truncate font-mono text-[11px] font-semibold text-foreground">
                  {segment.value || '-'}
                </span>
                <span className="truncate text-[10px] text-muted-foreground">
                  {formatDuration(segment.duration)}
                </span>
              </button>
            );
          })}
        </div>
        {cursorLeft && (
          <span
            className="pointer-events-none absolute inset-y-0 w-px bg-signal-warning shadow-[0_0_0_1px_var(--background)]"
            style={{ left: cursorLeft }}
          />
        )}
      </div>
      <div className="mt-2 flex items-center justify-between font-mono text-[10px] text-muted-foreground">
        <span>0с</span>
        <span>{formatDuration(total)}</span>
      </div>
    </div>
  );
}

/** Мини-спарклайн без осей и тултипа — для встраивания в таблицу. */
export function ScenarioSparkline({
  scenario,
  height = 28,
  width = 120,
  tagType = 'double',
}: {
  scenario: TagScenarioConfig;
  height?: number;
  width?: number;
  tagType?: TagType;
}) {
  if (tagType === 'string') {
    return <ScenarioStringSparkline scenario={scenario} height={height} width={width} />;
  }

  return <ScenarioNumericSparkline scenario={scenario} height={height} width={width} />;
}

function ScenarioNumericSparkline({
  scenario,
  height,
  width,
}: {
  scenario: TagScenarioConfig;
  height: number;
  width: number;
}) {
  const pts = useMemo(() => sampleScenario(scenario, 60), [scenario]);
  if (pts.length === 0) return <span className="text-muted-foreground">-</span>;
  const xs = pts.map((p) => p.t);
  const ys = pts.map((p) => p.value);
  const xMin = Math.min(...xs),
    xMax = Math.max(...xs);
  const yMin = Math.min(...ys),
    yMax = Math.max(...ys);
  const dx = xMax - xMin || 1;
  const dy = yMax - yMin || 1;
  const path = pts
    .map((p, i) => {
      const x = ((p.t - xMin) / dx) * width;
      const y = height - ((p.value - yMin) / dy) * (height - 2) - 1;
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
  return (
    <svg width={width} height={height} className="overflow-visible">
      <path d={path} fill="none" stroke="oklch(0.78 0.16 195)" strokeWidth={1.5} />
    </svg>
  );
}

function ScenarioStringSparkline({
  scenario,
  height,
  width,
}: {
  scenario: TagScenarioConfig;
  height: number;
  width: number;
}) {
  const segments = useMemo(() => scenarioTextSegments(scenario), [scenario]);
  const total = totalDuration(scenario);
  if (segments.length === 0) return <span className="text-muted-foreground">-</span>;

  let x = 0;
  const tooltip = segments
    .map((segment) => `${segment.label || `#${segment.segmentIdx + 1}`}: ${segment.value || '-'}`)
    .join(' · ');

  return (
    <svg width={width} height={height} className="overflow-visible" title={tooltip}>
      {segments.map((segment) => {
        const rectWidth = (segment.duration / Math.max(total, 1)) * width;
        const rectX = x;
        x += rectWidth;
        return (
          <rect
            key={segment.id}
            x={rectX}
            y={Math.max(1, height / 2 - 4)}
            width={Math.max(1, rectWidth - 1)}
            height={8}
            rx={2}
            fill={segmentColor(segment.segmentIdx)}
            opacity={0.72}
          />
        );
      })}
    </svg>
  );
}
