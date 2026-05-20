import type { CalcType } from '@/types/uniemu';

export const GENERATOR_CALC_TYPES = [
  'Line',
  'Curve',
  'Random',
  'Sinusoid',
  'Square',
  'Sawtooth',
  'SquircleEarly',
  'SquircleLate',
] as const satisfies readonly CalcType[];

export const SCENARIO_CALC_TYPES = [
  'Static',
  'Line',
  'Curve',
  'Random',
  'Sinusoid',
  'Square',
  'Sawtooth',
  'SquircleEarly',
  'SquircleLate',
] as const satisfies readonly CalcType[];
