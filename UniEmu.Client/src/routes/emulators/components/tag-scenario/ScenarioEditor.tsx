import { useEffect, useMemo, useState } from 'react';
import {
  ArrowDown,
  ArrowUp,
  ChevronDown,
  ChevronRight,
  Copy,
  Plus,
  Repeat,
  Search,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ScrollArea } from '@/components/ui/scroll-area';
import { cn } from '@/lib/utils';
import type { TagScenarioConfig, TagScenarioSegment } from '@/types/uniemu';
import { CalcConfigFields } from './CalcConfigFields';
import { ScenarioPreviewChart, ScenarioSparkline } from './ScenarioPreviewChart';
import { defaultSegment, formatDuration, totalDuration, valueAt } from './scenarioMath';
import { localization } from '@/localization';

interface Props {
  value: TagScenarioConfig;
  onChange: (next: TagScenarioConfig) => void;
}

type DurUnit = 'sec' | 'min';

function splitDuration(sec: number): { v: number; u: DurUnit } {
  if (sec >= 60 && sec % 60 === 0) return { v: sec / 60, u: 'min' };
  return { v: sec, u: 'sec' };
}

function cloneSegment(seg: TagScenarioSegment): TagScenarioSegment {
  return {
    ...seg,
    id: `seg-${Math.random().toString(36).slice(2, 8)}`,
    calc: { ...seg.calc },
  };
}

export function ScenarioEditor({ value, onChange }: Props) {
  const segments = value.segments;

  const [selectedId, setSelectedId] = useState<string | null>(segments[0]?.id ?? null);
  const [query, setQuery] = useState('');
  const [allOpen, setAllOpen] = useState(false);
  const [cursorSec, setCursorSec] = useState<number | null>(null);

  // Поддерживаем валидное выделение
  useEffect(() => {
    if (selectedId && segments.some((s) => s.id === selectedId)) return;
    setSelectedId(segments[0]?.id ?? null);
  }, [segments, selectedId]);

  const selectedIdx = segments.findIndex((s) => s.id === selectedId);
  const selected = selectedIdx >= 0 ? segments[selectedIdx] : null;

  const update = (patch: Partial<TagScenarioConfig>) => onChange({ ...value, ...patch });
  const updateSeg = (idx: number, patch: Partial<TagScenarioSegment>) =>
    update({ segments: segments.map((s, i) => (i === idx ? { ...s, ...patch } : s)) });
  const removeSeg = (idx: number) => {
    const next = segments.filter((_, i) => i !== idx);
    update({ segments: next });
    if (segments[idx]?.id === selectedId) {
      setSelectedId(next[Math.max(0, idx - 1)]?.id ?? null);
    }
  };
  const duplicateSeg = (idx: number) => {
    const copy = cloneSegment(segments[idx]);
    const next = [...segments.slice(0, idx + 1), copy, ...segments.slice(idx + 1)];
    update({ segments: next });
    setSelectedId(copy.id);
  };
  const move = (idx: number, dir: -1 | 1) => {
    const next = [...segments];
    const t = idx + dir;
    if (t < 0 || t >= next.length) return;
    [next[idx], next[t]] = [next[t], next[idx]];
    update({ segments: next });
  };
  const add = () => {
    const seg = defaultSegment();
    update({ segments: [...segments, seg] });
    setSelectedId(seg.id);
  };

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return segments.map((s, i) => ({ seg: s, idx: i }));
    return segments
      .map((s, i) => ({ seg: s, idx: i }))
      .filter(
        ({ seg }) =>
          (seg.label ?? '').toLowerCase().includes(q) || seg.calc.type.toLowerCase().includes(q)
      );
  }, [segments, query]);

  const total = totalDuration(value);
  const cumOffsets = useMemo(() => {
    const out: number[] = [];
    let acc = 0;
    for (const s of segments) {
      out.push(acc);
      acc += s.duration;
    }
    return out;
  }, [segments]);

  // Клик по графику → выбираем соответствующий сегмент
  const handleChartClick = (tSec: number) => {
    if (segments.length === 0) return;
    setCursorSec(tSec);
    let acc = 0;
    for (const s of segments) {
      if (tSec <= acc + s.duration) {
        setSelectedId(s.id);
        return;
      }
      acc += s.duration;
    }
    setSelectedId(segments[segments.length - 1].id);
  };

  const cursorValue = cursorSec !== null && segments.length > 0 ? valueAt(value, cursorSec) : null;

  return (
    <div className="space-y-3">
      {/* Шапка: статистика + loop */}
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border bg-muted/10 px-3 py-2">
        <div className="flex items-center gap-3 text-[11px] text-muted-foreground">
          <span>
            {localization.routes.emulators.components.tagScenario.scenarioEditor.segmentsCountLabel(segments.length)}
          </span>
          <span>·</span>
          <span>
            Σ <span className="font-mono text-foreground">{formatDuration(total)}</span>
          </span>
          {cursorSec !== null && cursorValue !== null && (
            <>
              <span>·</span>
              <span>
                t=<span className="font-mono text-foreground">{formatDuration(cursorSec)}</span> v=
                <span className="font-mono text-foreground">{cursorValue.toFixed(2)}</span>
              </span>
            </>
          )}
        </div>
        <Label className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <Repeat className="h-3 w-3" />{' '}
          {localization.routes.emulators.components.tagScenario.scenarioEditor.endBehaviorLabel}
          <Select
            value={value.continueOnFormulaEnd ?? 'Repeat'}
            onValueChange={(v) =>
              update({ continueOnFormulaEnd: v as TagScenarioConfig['continueOnFormulaEnd'] })
            }
          >
            <SelectTrigger className="h-7 w-[130px] text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="NoSignal" className="text-xs">
                {localization.routes.emulators.components.tagScenario.scenarioEditor.endBehaviorNoSignalLabel}
              </SelectItem>
              <SelectItem value="Zero" className="text-xs">
                {localization.routes.emulators.components.tagScenario.scenarioEditor.endBehaviorZeroLabel}
              </SelectItem>
              <SelectItem value="Repeat" className="text-xs">
                {localization.routes.emulators.components.tagScenario.scenarioEditor.endBehaviorRepeatLabel}
              </SelectItem>
              <SelectItem value="Stretch" className="text-xs">
                {localization.routes.emulators.components.tagScenario.scenarioEditor.endBehaviorHoldLabel}
              </SelectItem>
            </SelectContent>
          </Select>
        </Label>
      </div>

      {/* Двухпанельный layout */}
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(260px,320px)_1fr]">
        {/* ЛЕВО — список сегментов */}
        <div className="flex min-h-0 flex-col rounded-md border border-border bg-background/40">
          <div className="border-b border-border p-2 space-y-2">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={
                  localization.routes.emulators.components.tagScenario.scenarioEditor.searchPlaceholder
                }
                className="h-8 pl-7 text-xs"
              />
            </div>
            <div className="flex items-center justify-between gap-2">
              <Button
                variant="ghost"
                size="sm"
                className="h-7 gap-1 px-2 text-[11px] text-muted-foreground"
                onClick={() => setAllOpen((v) => !v)}
                title={
                  allOpen
                    ? localization.routes.emulators.components.tagScenario.scenarioEditor.collapseAllButtonLabel
                    : localization.routes.emulators.components.tagScenario.scenarioEditor.expandAllButtonLabel
                }
              >
                {allOpen ? (
                  <ChevronDown className="h-3 w-3" />
                ) : (
                  <ChevronRight className="h-3 w-3" />
                )}
                {allOpen
                  ? localization.routes.emulators.components.tagScenario.scenarioEditor.collapseAllTooltip
                  : localization.routes.emulators.components.tagScenario.scenarioEditor.expandAllTooltip}
              </Button>
              <Button variant="outline" size="sm" className="h-7 gap-1 px-2 text-xs" onClick={add}>
                <Plus className="h-3 w-3" />{' '}
                {localization.routes.emulators.components.tagScenario.scenarioEditor.addSegmentButtonLabel}
              </Button>
            </div>
          </div>

          <ScrollArea className="max-h-[420px] flex-1">
            <ul className="p-1.5">
              {filtered.length === 0 && (
                <li className="px-2 py-6 text-center text-[11px] text-muted-foreground">
                  {localization.routes.emulators.components.tagScenario.scenarioEditor.emptySearchMessage}
                </li>
              )}
              {filtered.map(({ seg, idx }) => {
                const isSelected = seg.id === selectedId;
                const startAt = cumOffsets[idx] ?? 0;
                return (
                  <li key={seg.id}>
                    <button
                      type="button"
                      onClick={() => setSelectedId(seg.id)}
                      className={cn(
                        'group block w-full rounded-md border border-transparent px-2 py-1.5 text-left transition-colors',
                        isSelected ? 'border-primary/40 bg-primary/10' : 'hover:bg-muted/30'
                      )}
                    >
                      <div className="mb-1 flex items-center justify-between gap-2">
                        <div className="flex items-center gap-1.5 min-w-0">
                          <span className="rounded bg-primary/15 px-1 py-0.5 font-mono text-[10px] font-semibold text-primary">
                            #{idx + 1}
                          </span>
                          <span className="truncate text-xs font-medium">
                            {seg.label || (
                              <span className="text-muted-foreground">
                                {
                                  localization.routes.emulators.components.tagScenario
                                    .scenarioEditor.text15
                                }
                              </span>
                            )}
                          </span>
                        </div>
                        <div className="flex items-center gap-0.5 opacity-0 transition-opacity group-hover:opacity-100">
                          <Button asChild size="icon" variant="ghost" className="h-6 w-6">
                            <span
                              role="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                move(idx, -1);
                              }}
                              aria-disabled={idx === 0}
                              title={
                                localization.routes.emulators.components.tagScenario.scenarioEditor
                                  .text16
                              }
                            >
                              <ArrowUp className="h-3 w-3" />
                            </span>
                          </Button>
                          <Button asChild size="icon" variant="ghost" className="h-6 w-6">
                            <span
                              role="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                move(idx, 1);
                              }}
                              aria-disabled={idx === segments.length - 1}
                              title={
                                localization.routes.emulators.components.tagScenario.scenarioEditor
                                  .text17
                              }
                            >
                              <ArrowDown className="h-3 w-3" />
                            </span>
                          </Button>
                          <Button asChild size="icon" variant="ghost" className="h-6 w-6">
                            <span
                              role="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                duplicateSeg(idx);
                              }}
                              title={
                                localization.routes.emulators.components.tagScenario.scenarioEditor
                                  .text18
                              }
                            >
                              <Copy className="h-3 w-3" />
                            </span>
                          </Button>
                          <Button
                            asChild
                            size="icon"
                            variant="ghost"
                            className="h-6 w-6 hover:text-signal-offline"
                          >
                            <span
                              role="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                removeSeg(idx);
                              }}
                              title={
                                localization.routes.emulators.components.tagScenario.scenarioEditor
                                  .text19
                              }
                            >
                              <Trash2 className="h-3 w-3" />
                            </span>
                          </Button>
                        </div>
                      </div>
                      <div className="flex items-center justify-between gap-2 text-[10px] text-muted-foreground">
                        <span className="font-mono">
                          {seg.calc.type} · {formatDuration(seg.duration)}
                        </span>
                        <span className="font-mono">t₀ {formatDuration(startAt)}</span>
                      </div>
                      {allOpen && (
                        <div className="mt-1.5">
                          <ScenarioSparkline
                            scenario={{ segments: [seg], continueOnFormulaEnd: 'NoSignal' }}
                            height={22}
                            width={260}
                          />
                        </div>
                      )}
                    </button>
                  </li>
                );
              })}
            </ul>
          </ScrollArea>
        </div>

        {/* ПРАВО — sticky-превью + форма выбранного сегмента */}
        <div className="space-y-3">
          <div className="sticky top-0 z-10 rounded-md border border-border bg-background/80 p-2 backdrop-blur">
            <ScenarioPreviewChart
              scenario={value}
              height={160}
              cursorSec={cursorSec}
              onPointClick={handleChartClick}
              highlightSegmentIdx={selectedIdx}
            />
          </div>

          {selected ? (
            <div className="rounded-md border border-border bg-muted/10 p-3">
              <div className="mb-3 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="rounded bg-primary/15 px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-primary">
                    #{selectedIdx + 1}
                  </span>
                  <span className="font-mono text-xs text-muted-foreground">
                    {selected.calc.type} · {formatDuration(selected.duration)}
                  </span>
                </div>
                <div className="flex gap-0.5">
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7"
                    onClick={() => duplicateSeg(selectedIdx)}
                    title={
                      localization.routes.emulators.components.tagScenario.scenarioEditor.duplicateMenuLabel
                    }
                  >
                    <Copy className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7"
                    onClick={() => move(selectedIdx, -1)}
                    disabled={selectedIdx === 0}
                    title={
                      localization.routes.emulators.components.tagScenario.scenarioEditor.moveUpMenuLabel
                    }
                  >
                    <ArrowUp className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7"
                    onClick={() => move(selectedIdx, 1)}
                    disabled={selectedIdx === segments.length - 1}
                    title={
                      localization.routes.emulators.components.tagScenario.scenarioEditor.moveDownMenuLabel
                    }
                  >
                    <ArrowDown className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-7 w-7 hover:text-signal-offline"
                    onClick={() => removeSeg(selectedIdx)}
                    title={
                      localization.routes.emulators.components.tagScenario.scenarioEditor.deleteMenuLabel
                    }
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>

              <div className="mb-3 grid grid-cols-3 gap-2">
                <div className="col-span-1 space-y-1">
                  <Label className="text-[11px]">
                    {localization.routes.emulators.components.tagScenario.scenarioEditor.labelFieldLabel}
                  </Label>
                  <Input
                    value={selected.label ?? ''}
                    onChange={(e) => updateSeg(selectedIdx, { label: e.target.value })}
                    placeholder={
                      localization.routes.emulators.components.tagScenario.scenarioEditor.labelPlaceholder
                    }
                    className="h-8 text-xs"
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-[11px]">
                    {localization.routes.emulators.components.tagScenario.scenarioEditor.durationLabel}
                  </Label>
                  <Input
                    type="number"
                    min={0}
                    value={splitDuration(selected.duration).v}
                    onChange={(e) => {
                      const nv = Number(e.target.value) || 0;
                      const u = splitDuration(selected.duration).u;
                      updateSeg(selectedIdx, { duration: u === 'min' ? nv * 60 : nv });
                    }}
                    className="h-8 font-mono text-xs"
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-[11px]">
                    {localization.routes.emulators.components.tagScenario.scenarioEditor.durationUnitLabel}
                  </Label>
                  <Select
                    value={splitDuration(selected.duration).u}
                    onValueChange={(nu) => {
                      const cur = splitDuration(selected.duration).v;
                      updateSeg(selectedIdx, { duration: nu === 'min' ? cur * 60 : cur });
                    }}
                  >
                    <SelectTrigger className="h-8 text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="sec">
                        {localization.routes.emulators.components.tagScenario.scenarioEditor.secondsUnitLabel}
                      </SelectItem>
                      <SelectItem value="min">
                        {localization.routes.emulators.components.tagScenario.scenarioEditor.minutesUnitLabel}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <CalcConfigFields
                value={selected.calc}
                onChange={(calc) => updateSeg(selectedIdx, { calc })}
                hideDuration
                compact
              />
            </div>
          ) : (
            <div className="rounded-md border border-dashed border-border bg-muted/10 p-6 text-center text-xs text-muted-foreground">
              {localization.routes.emulators.components.tagScenario.scenarioEditor.emptyTimelineMessage}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
