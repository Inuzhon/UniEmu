import type { TagCalcConfig, TagScenarioConfig, TagScenarioSegment } from "@/types/uniemu";

export interface ScenarioPoint {
  /** seconds from scenario start */
  t: number;
  value: number;
  /** segment index this point belongs to */
  segmentIdx: number;
}

const num = (s: string | undefined, fallback = 0) => {
  if (s === undefined || s === "") return fallback;
  const n = Number(s);
  return Number.isFinite(n) ? n : fallback;
};

/** Семплирует одну точку сегмента, локальное время u ∈ [0..1]. */
function sampleCalcAt(calc: TagCalcConfig, u: number, durationSec: number, prevValue: number): number {
  const start = num(calc.start, 0);
  const finish = num(calc.finish, 0);
  const amp = calc.amplitude ?? 1;
  const period = Math.max(calc.period ?? 1, 1);
  const distortion = (calc.distortion ?? 0) / 100;
  const tSec = u * durationSec;

  let v: number;
  switch (calc.type) {
    case "Line":
      v = start + (finish - start) * u;
      break;
    case "Curve": {
      const k = calc.curvature ?? 2;
      v = start + (finish - start) * Math.pow(u, k);
      break;
    }
    case "SquircleEarly":
      v = start + (finish - start) * (1 - Math.pow(1 - u, 2));
      break;
    case "SquircleLate":
      v = start + (finish - start) * Math.pow(u, 2);
      break;
    case "Random": {
      const lo = Math.min(start, finish);
      const hi = Math.max(start, finish);
      // deterministic-ish: hash u
      const h = Math.sin(u * 1234.567 + start * 7.13 + finish * 3.71) * 0.5 + 0.5;
      v = lo + (hi - lo) * h;
      break;
    }
    case "Sinusoid": {
      const center = num(calc.start, 0);
      v = center + amp * Math.sin((2 * Math.PI * tSec) / Math.max(period, 1));
      break;
    }
    case "Square": {
      const center = num(calc.start, 0);
      const phase = (tSec / Math.max(period, 1)) % 1;
      v = center + (phase < 0.5 ? amp : -amp);
      break;
    }
    case "Sawtooth": {
      const center = num(calc.start, 0);
      const phase = (tSec / Math.max(period, 1)) % 1;
      v = center + amp * (2 * phase - 1);
      break;
    }
    // case "Sequence": {
    //   try {
    //     const arr = JSON.parse(calc.start ?? "[]");
    //     if (Array.isArray(arr) && arr.length > 0) {
    //       const idx = Math.min(arr.length - 1, Math.floor(u * arr.length));
    //       const n = Number(arr[idx]);
    //       v = Number.isFinite(n) ? n : prevValue;
    //     }
    //   } catch {
    //     v = prevValue;
    //   }
    //   break;
    // }
    case "Text":
    case "None":
    default:
      v = prevValue;
  }

  if (distortion > 0) {
    const noise = (Math.sin(tSec * 53.13 + u * 17.7) * 0.5) * distortion;
    const scale = Math.max(1, Math.abs(v));
    v = v + noise * scale;
  }
  return v;
}

/**
 * Возвращает массив точек по всем сегментам сценария.
 * @param totalSamples - желаемое суммарное число точек.
 */
export function sampleScenario(
  scenario: TagScenarioConfig,
  totalSamples = 200,
): ScenarioPoint[] {
  const segs = scenario.segments.filter((s) => s.duration > 0);
  if (segs.length === 0) return [];
  const totalDur = segs.reduce((a, b) => a + b.duration, 0);
  const pts: ScenarioPoint[] = [];
  let tOffset = 0;
  let prev = num(scenario.startValue, num(segs[0].calc.start, 0));

  segs.forEach((seg, segIdx) => {
    const samples = Math.max(2, Math.round((seg.duration / totalDur) * totalSamples));
    for (let i = 0; i < samples; i++) {
      const u = i / (samples - 1);
      const v = sampleCalcAt(seg.calc, u, seg.duration, prev);
      pts.push({ t: tOffset + u * seg.duration, value: v, segmentIdx: segIdx });
      if (i === samples - 1) prev = v;
    }
    tOffset += seg.duration;
  });

  return pts;
}

/** Значение сценария в момент t (сек). При Repeat — wraps; иначе — поведение по continueOnFormulaEnd. */
export function valueAt(scenario: TagScenarioConfig, tSec: number): number | null {
  const segs = scenario.segments.filter((s) => s.duration > 0);
  if (segs.length === 0) return null;
  const total = segs.reduce((a, b) => a + b.duration, 0);
  const mode = scenario.continueOnFormulaEnd ?? "Repeat";
  let t = tSec;
  if (mode === "Repeat") t = ((t % total) + total) % total;
  else if (t < 0) t = 0;
  else if (t > total) {
    if (mode === "NoSignal") return null;
    if (mode === "Zero") return 0;
    t = total; // Stretch — удерживаем последнее значение
  }

  let acc = 0;
  let prev = num(scenario.startValue, num(segs[0].calc.start, 0));
  for (const seg of segs) {
    if (t <= acc + seg.duration) {
      const u = (t - acc) / seg.duration;
      return sampleCalcAt(seg.calc, u, seg.duration, prev);
    }
    // advance prev to end of this segment
    prev = sampleCalcAt(seg.calc, 1, seg.duration, prev);
    acc += seg.duration;
  }
  return prev;
}

export function totalDuration(scenario: TagScenarioConfig): number {
  return scenario.segments.reduce((a, b) => a + (b.duration || 0), 0);
}

export function formatDuration(sec: number): string {
  if (sec < 60) return `${sec.toFixed(0)}с`;
  const m = Math.floor(sec / 60);
  const s = Math.round(sec % 60);
  if (m < 60) return s ? `${m}м ${s}с` : `${m}м`;
  const h = Math.floor(m / 60);
  return `${h}ч ${m % 60}м`;
}

export function defaultSegment(): TagScenarioSegment {
  return {
    id: `seg-${Math.random().toString(36).slice(2, 8)}`,
    duration: 10,
    calc: { type: "Line", start: "0", finish: "100", duration: 10, distortion: 0 },
  };
}
