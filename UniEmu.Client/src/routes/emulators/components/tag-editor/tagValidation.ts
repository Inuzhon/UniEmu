import type { CalcType, SpecialParameter, TagCalcConfig, TagSource, TagType } from '@/types/uniemu';
import { GENERATOR_CALC_TYPES, SCENARIO_CALC_TYPES } from '../tag-scenario/calcTypeOptions';
import type { TagEditorFormState } from './types';

const TAG_NAME_MAX_LENGTH = 200;
const TAG_KEY_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 1000;
const TEXT_VALUE_MAX_LENGTH = 2000;
const SCRIPT_ID_MAX_LENGTH = 64;
const INLINE_SCRIPT_MAX_LENGTH = 200_000;
const MAX_NUMERIC_MAGNITUDE = 1_000_000_000;
export const MIN_CALC_DURATION_SECONDS = 1;
export const MAX_CALC_DURATION_SECONDS = 86_400;
export const MAX_SCENARIO_SEGMENTS = 200;
export const MAX_SCENARIO_TOTAL_DURATION_SECONDS = 604_800;
export const MAX_DISTORTION_PERCENT = 100;
export const MIN_CURVATURE = 0.1;
export const MAX_CURVATURE = 20;

export const TEXT_SPECIAL_PARAMETERS = [
  'PrgName',
  'FrameText',
  'ErrorText',
  'Message',
  'CNCModel',
  'FirmwareVersion',
  'SerialNumber',
  'PLCVersion',
  'Subprogram',
] as const satisfies readonly SpecialParameter[];

const TAG_TYPES = ['int', 'double', 'string', 'bool'] as const satisfies readonly TagType[];
const NUMERIC_TAG_TYPES = ['int', 'double'] as const satisfies readonly TagType[];
const NON_NUMERIC_SCENARIO_CALC_TYPES = ['Static'] as const satisfies readonly CalcType[];
const RANGE_CALC_TYPES = [
  'Line',
  'Curve',
  'Random',
  'SquircleEarly',
  'SquircleLate',
] as const satisfies readonly CalcType[];
const DURATION_CALC_TYPES = [
  'Line',
  'Curve',
  'SquircleEarly',
  'SquircleLate',
] as const satisfies readonly CalcType[];
const WAVE_CALC_TYPES = ['Sinusoid', 'Square', 'Sawtooth'] as const satisfies readonly CalcType[];
const NUMERIC_TAG_SOURCES = [
  'static',
  'generator',
  'scenario',
  'formulaScript',
  'script',
] as const satisfies readonly TagSource[];
const KEY_WHITESPACE_RE = /\s/;
const INTEGER_RE = /^[-+]?\d+$/;
const NUMBER_RE = /^[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[-+]?\d+)?$/i;
const NON_NUMERIC_TAG_SOURCES = [
  'static',
  'scenario',
  'formulaScript',
  'script',
] as const satisfies readonly TagSource[];

export function isNumericTagType(type: TagType) {
  return NUMERIC_TAG_TYPES.includes(type);
}

export function getRequiredTagType(specialParameter: SpecialParameter): TagType | null {
  if (TEXT_SPECIAL_PARAMETERS.includes(specialParameter)) {
    return 'string';
  }

  if (specialParameter === 'FrameNum') {
    return 'int';
  }

  return null;
}

export function getAllowedTagTypes(specialParameter: SpecialParameter): readonly TagType[] {
  const requiredType = getRequiredTagType(specialParameter);
  return requiredType ? [requiredType] : TAG_TYPES;
}

export function getAllowedTagSources(type: TagType): readonly TagSource[] {
  return isNumericTagType(type) ? NUMERIC_TAG_SOURCES : NON_NUMERIC_TAG_SOURCES;
}

export function getScenarioCalcTypes(type: TagType): readonly CalcType[] {
  return isNumericTagType(type) ? SCENARIO_CALC_TYPES : NON_NUMERIC_SCENARIO_CALC_TYPES;
}

export function clampDistortionPercent(value: number) {
  return clampFiniteNumber(value, 0, MAX_DISTORTION_PERCENT, 0);
}

export function clampDurationSeconds(value: number) {
  return Math.round(
    clampFiniteNumber(value, MIN_CALC_DURATION_SECONDS, MAX_CALC_DURATION_SECONDS, 1)
  );
}

export function clampPeriodSeconds(value: number) {
  return clampFiniteNumber(value, MIN_CALC_DURATION_SECONDS, MAX_CALC_DURATION_SECONDS, 1);
}

export function clampCurvature(value: number) {
  return clampFiniteNumber(value, MIN_CURVATURE, MAX_CURVATURE, 2);
}

export function clampNonNegativeMagnitude(value: number) {
  return clampFiniteNumber(value, 0, MAX_NUMERIC_MAGNITUDE, 0);
}

export function clampIntervalValue(value: number) {
  return Math.round(clampFiniteNumber(value, 1, MAX_CALC_DURATION_SECONDS, 1));
}

export function normalizeTagEditorForm(form: TagEditorFormState): TagEditorFormState {
  let next = form;
  const requiredType = getRequiredTagType(next.specialParameter);

  if (requiredType && next.type !== requiredType) {
    next = {
      ...next,
      type: requiredType,
      staticValue: normalizeStaticValue(requiredType, next.staticValue),
    };
  }

  if (!getAllowedTagSources(next.type).includes(next.source)) {
    next = { ...next, source: 'static' };
  }

  if (next.type !== 'double' && next.roundEnabled) {
    next = { ...next, roundEnabled: false };
  }

  const normalizedScenario = normalizeScenario(next.type, next.scenario);
  if (normalizedScenario !== next.scenario) {
    next = { ...next, scenario: normalizedScenario };
  }

  let normalizedCalc = next.calc;
  if (!GENERATOR_CALC_TYPES.includes(normalizedCalc.type)) {
    normalizedCalc = { ...normalizedCalc, type: 'Line' };
  }
  normalizedCalc = normalizeCalc(normalizedCalc);
  if (normalizedCalc !== next.calc) {
    next = { ...next, calc: normalizedCalc };
  }

  return next;
}

export function getTagValidationErrors(form: TagEditorFormState): string[] {
  const errors: string[] = [];
  const requiredType = getRequiredTagType(form.specialParameter);
  const normalizedName = form.name.trim();
  const normalizedKey = form.key.trim();

  if (normalizedName.length > TAG_NAME_MAX_LENGTH) {
    errors.push(`Имя тега не должно быть длиннее ${TAG_NAME_MAX_LENGTH} символов.`);
  }

  if (normalizedKey.length > TAG_KEY_MAX_LENGTH) {
    errors.push(`Ключ тега не должен быть длиннее ${TAG_KEY_MAX_LENGTH} символов.`);
  } else if (normalizedKey.length > 0 && KEY_WHITESPACE_RE.test(normalizedKey)) {
    errors.push('Ключ тега не должен содержать пробелы или переносы строк.');
  }

  if (form.description.length > DESCRIPTION_MAX_LENGTH) {
    errors.push(`Описание тега не должно быть длиннее ${DESCRIPTION_MAX_LENGTH} символов.`);
  }

  if (requiredType && form.type !== requiredType) {
    errors.push(
      requiredType === 'int'
        ? 'Выбранный спецпараметр требует целочисленный тип данных.'
        : 'Выбранный спецпараметр требует строковый тип данных.'
    );
  }

  if (!getAllowedTagSources(form.type).includes(form.source)) {
    errors.push('Выбранный источник данных недоступен для этого типа данных.');
  }

  if (form.source === 'static') {
    appendTypedValueError(errors, form.type, form.staticValue, 'Статическое значение тега');
  }

  appendTriggerErrors(errors, form);

  if (form.source === 'generator' || form.source === 'formulaScript') {
    appendGeneratorCalcErrors(errors, form.calc);
  }

  if (form.source === 'script' || form.source === 'formulaScript') {
    appendFormulaErrors(errors, form);
  }

  if (form.source === 'scenario') {
    appendScenarioErrors(errors, form);
  }

  return errors;
}

function normalizeCalc(calc: TagCalcConfig): TagCalcConfig {
  let next = calc;
  const set = <K extends keyof TagCalcConfig>(key: K, value: TagCalcConfig[K]) => {
    if (!Object.is(next[key], value)) {
      next = { ...next, [key]: value };
    }
  };

  if (calc.distortion !== undefined) {
    set('distortion', clampDistortionPercent(calc.distortion));
  }

  if (DURATION_CALC_TYPES.includes(calc.type)) {
    set('duration', clampDurationSeconds(calc.duration ?? 1));
  }

  if (WAVE_CALC_TYPES.includes(calc.type)) {
    set('period', clampPeriodSeconds(calc.period ?? 1));
    if (calc.amplitude !== undefined) {
      set('amplitude', clampNonNegativeMagnitude(calc.amplitude));
    }
  }

  if (calc.type === 'Curve' && calc.curvature !== undefined) {
    set('curvature', clampCurvature(calc.curvature));
  }

  return next;
}

function normalizeScenario(
  type: TagType,
  scenario: TagEditorFormState['scenario']
): TagEditorFormState['scenario'] {
  const allowed = getScenarioCalcTypes(type);
  let changed = false;
  const segments = scenario.segments.map((segment) => {
    let nextSegment = segment;
    const duration = clampDurationSeconds(segment.duration);
    if (duration !== segment.duration) {
      changed = true;
      nextSegment = { ...nextSegment, duration };
    }

    const calc = allowed.includes(nextSegment.calc.type)
      ? normalizeCalc(nextSegment.calc)
      : { ...nextSegment.calc, type: 'Static' as CalcType };

    if (calc !== nextSegment.calc) {
      changed = true;
      nextSegment = { ...nextSegment, calc };
    }

    return nextSegment;
  });

  return changed ? { ...scenario, segments } : scenario;
}

function normalizeStaticValue(type: TagType, value: string) {
  if (type === 'bool') {
    return value === 'true' ? 'true' : 'false';
  }

  return value;
}

function appendTriggerErrors(errors: string[], form: TagEditorFormState) {
  if (form.triggerMode === 'interval') {
    if (!Number.isFinite(form.intervalValue) || form.intervalValue < 1) {
      errors.push('Интервал вычисления тега должен быть больше нуля.');
    }
  }

  if (form.triggerMode === 'cron') {
    const parts = form.cron.trim().split(/\s+/).filter(Boolean);
    if (parts.length < 5 || parts.length > 7) {
      errors.push('Cron-выражение тега некорректно.');
    }
  }
}

function appendFormulaErrors(errors: string[], form: TagEditorFormState) {
  const hasScriptId = form.scriptId.trim().length > 0;
  const hasInlineScript = form.inlineScript.trim().length > 0;
  if (!hasScriptId && !hasInlineScript) {
    errors.push(
      'Для скриптового источника выберите сохраненный скрипт или заполните inline-скрипт.'
    );
  }

  if (form.scriptId.length > SCRIPT_ID_MAX_LENGTH) {
    errors.push(`Идентификатор скрипта не должен быть длиннее ${SCRIPT_ID_MAX_LENGTH} символов.`);
  }

  if (form.inlineScript.length > INLINE_SCRIPT_MAX_LENGTH) {
    errors.push(`Inline-скрипт не должен быть длиннее ${INLINE_SCRIPT_MAX_LENGTH} символов.`);
  }
}

function appendGeneratorCalcErrors(errors: string[], calc: TagCalcConfig) {
  if (!GENERATOR_CALC_TYPES.includes(calc.type)) {
    errors.push(`Формула расчета ${calc.type} недоступна для генератора.`);
    return;
  }

  appendNumericCalcErrors(errors, calc);
}

function appendScenarioErrors(errors: string[], form: TagEditorFormState) {
  const scenarioCalcTypes = getScenarioCalcTypes(form.type);
  if (form.scenario.segments.length === 0) {
    errors.push('Сценарий должен содержать хотя бы один участок.');
    return;
  }

  if (form.scenario.segments.length > MAX_SCENARIO_SEGMENTS) {
    errors.push(`Сценарий не должен содержать больше ${MAX_SCENARIO_SEGMENTS} участков.`);
  }

  if (form.scenario.startValue) {
    appendTypedValueError(
      errors,
      form.type,
      form.scenario.startValue,
      'Начальное значение сценария'
    );
  }

  let totalDuration = 0;
  form.scenario.segments.forEach((segment, index) => {
    const label = `Участок сценария #${index + 1}`;
    if (!Number.isFinite(segment.duration) || segment.duration <= 0) {
      errors.push(`${label}: длительность должна быть больше нуля.`);
    } else if (segment.duration > MAX_CALC_DURATION_SECONDS) {
      errors.push(
        `${label}: длительность не должна превышать ${MAX_CALC_DURATION_SECONDS} секунд.`
      );
    }

    totalDuration += Number.isFinite(segment.duration) ? Math.max(0, segment.duration) : 0;

    if ((segment.label ?? '').length > 120) {
      errors.push(`${label}: метка не должна быть длиннее 120 символов.`);
    }

    if (!scenarioCalcTypes.includes(segment.calc.type)) {
      errors.push(`${label}: формула расчета недоступна для этого типа данных.`);
      return;
    }

    if (segment.calc.type === 'Static') {
      appendTypedValueError(errors, form.type, segment.calc.start ?? '', `${label}: значение`);
      return;
    }

    appendNumericCalcErrors(errors, segment.calc, `${label}: `);
  });

  if (totalDuration > MAX_SCENARIO_TOTAL_DURATION_SECONDS) {
    errors.push(
      `Суммарная длительность сценария не должна превышать ${MAX_SCENARIO_TOTAL_DURATION_SECONDS} секунд.`
    );
  }
}

function appendNumericCalcErrors(errors: string[], calc: TagCalcConfig, prefix = '') {
  if (
    calc.distortion !== undefined &&
    !isNumberInRange(calc.distortion, 0, MAX_DISTORTION_PERCENT)
  ) {
    errors.push(`${prefix}искажение (% шума) должно быть в диапазоне от 0 до 100.`);
  }

  if (RANGE_CALC_TYPES.includes(calc.type)) {
    appendNumberTextError(errors, calc.start, `${prefix}начальное значение формулы`);
    appendNumberTextError(errors, calc.finish, `${prefix}конечное значение формулы`);
  }

  if (DURATION_CALC_TYPES.includes(calc.type)) {
    if (!Number.isFinite(calc.duration) || (calc.duration ?? 0) <= 0) {
      errors.push(`${prefix}длительность формулы должна быть больше нуля.`);
    } else if ((calc.duration ?? 0) > MAX_CALC_DURATION_SECONDS) {
      errors.push(
        `${prefix}длительность формулы не должна превышать ${MAX_CALC_DURATION_SECONDS} секунд.`
      );
    }
  }

  if (WAVE_CALC_TYPES.includes(calc.type)) {
    appendNumberTextError(errors, calc.start, `${prefix}центр периодической формулы`);
    if (
      calc.amplitude !== undefined &&
      !isNumberInRange(calc.amplitude, 0, MAX_NUMERIC_MAGNITUDE)
    ) {
      errors.push(
        `${prefix}амплитуда периодической формулы должна быть конечным неотрицательным числом.`
      );
    }

    if (!isNumberInRange(calc.period, MIN_CALC_DURATION_SECONDS, MAX_CALC_DURATION_SECONDS)) {
      errors.push(`${prefix}период формулы должен быть больше нуля.`);
    }
  }

  if (
    calc.type === 'Curve' &&
    calc.curvature !== undefined &&
    !isNumberInRange(calc.curvature, MIN_CURVATURE, MAX_CURVATURE)
  ) {
    errors.push(`${prefix}кривизна формулы должна быть больше 0 и не больше ${MAX_CURVATURE}.`);
  }
}

function appendTypedValueError(errors: string[], type: TagType, value: string, fieldName: string) {
  if (type === 'int') {
    if (!isIntegerText(value)) {
      errors.push(`${fieldName} должно быть целым числом.`);
    }
    return;
  }

  if (type === 'double') {
    appendNumberTextError(errors, value, fieldName);
    return;
  }

  if (type === 'bool') {
    const normalized = value.trim().toLowerCase();
    if (!['true', 'false', '0', '1'].includes(normalized)) {
      errors.push(`${fieldName} должно быть логическим значением.`);
    }
    return;
  }

  if (value.length > TEXT_VALUE_MAX_LENGTH) {
    errors.push(`${fieldName} не должно быть длиннее ${TEXT_VALUE_MAX_LENGTH} символов.`);
  }
}

function appendNumberTextError(errors: string[], value: string | undefined, fieldName: string) {
  if (!isFiniteNumberText(value)) {
    errors.push(`${fieldName} должно быть конечным числом.`);
  }
}

function isIntegerText(value: string | undefined) {
  const normalized = value?.trim() ?? '';
  if (!INTEGER_RE.test(normalized)) return false;
  const parsed = Number(normalized);
  return Number.isSafeInteger(parsed) && parsed >= -2_147_483_648 && parsed <= 2_147_483_647;
}

function isFiniteNumberText(value: string | undefined) {
  const normalized = value?.trim() ?? '';
  if (!NUMBER_RE.test(normalized)) return false;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && Math.abs(parsed) <= MAX_NUMERIC_MAGNITUDE;
}

function isNumberInRange(value: number | undefined, min: number, max: number) {
  return value !== undefined && Number.isFinite(value) && value >= min && value <= max;
}

function clampFiniteNumber(value: number, min: number, max: number, fallback: number) {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(max, Math.max(min, value));
}
