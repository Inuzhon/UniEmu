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
import type { TagScenarioConfig } from '@/types/uniemu';
import { sampleScenario, totalDuration, formatDuration } from './scenarioMath';
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
}

const SEG_COLORS = [
  'oklch(0.78 0.16 195)',
  'oklch(0.82 0.16 80)',
  'oklch(0.78 0.18 155)',
  'oklch(0.78 0.16 320)',
  'oklch(0.82 0.16 30)',
];

export function ScenarioPreviewChart({
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
        {localization.routes.emulators.components.tagScenario.scenarioPreviewChart.text1}
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
                background: 'oklch(0.22 0.018 240)',
                border: '1px solid oklch(0.32 0.02 240)',
                borderRadius: 6,
                fontSize: 12,
              }}
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

/** Мини-спарклайн без осей и тултипа — для встраивания в таблицу. */
export function ScenarioSparkline({
  scenario,
  height = 28,
  width = 120,
}: {
  scenario: TagScenarioConfig;
  height?: number;
  width?: number;
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
