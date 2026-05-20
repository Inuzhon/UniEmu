import type { CalcType, SpecialParameter, TagSource, TagType } from '@/types/uniemu';
import { GENERATOR_CALC_TYPES, SCENARIO_CALC_TYPES } from '../tag-scenario/calcTypeOptions';
import type { TagEditorFormState } from './types';

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
const NUMERIC_TAG_SOURCES = [
  'static',
  'generator',
  'scenario',
  'formulaScript',
  'script',
] as const satisfies readonly TagSource[];
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

  if (!GENERATOR_CALC_TYPES.includes(next.calc.type)) {
    next = { ...next, calc: { ...next.calc, type: 'Line' } };
  }

  return next;
}

export function getTagValidationErrors(form: TagEditorFormState): string[] {
  const errors: string[] = [];
  const requiredType = getRequiredTagType(form.specialParameter);

  if (requiredType && form.type !== requiredType) {
    errors.push(
      requiredType === 'int'
        ? 'Выбранный спецпараметр требует целочисленный тип данных.'
        : 'Выбранный спецпараметр требует строковый тип данных.',
    );
  }

  if (!getAllowedTagSources(form.type).includes(form.source)) {
    errors.push('Выбранный источник данных недоступен для этого типа данных.');
  }

  const scenarioCalcTypes = getScenarioCalcTypes(form.type);
  if (
    form.source === 'scenario' &&
    form.scenario.segments.some((segment) => !scenarioCalcTypes.includes(segment.calc.type))
  ) {
    errors.push('Сценарий содержит формулу расчета, недоступную для этого типа данных.');
  }

  return errors;
}

function normalizeScenario(
  type: TagType,
  scenario: TagEditorFormState['scenario'],
): TagEditorFormState['scenario'] {
  const allowed = getScenarioCalcTypes(type);
  let changed = false;
  const segments = scenario.segments.map((segment) => {
    if (allowed.includes(segment.calc.type)) {
      return segment;
    }

    changed = true;
    return {
      ...segment,
      calc: {
        ...segment.calc,
        type: 'Static' as CalcType,
      },
    };
  });

  return changed ? { ...scenario, segments } : scenario;
}

function normalizeStaticValue(type: TagType, value: string) {
  if (type === 'bool') {
    return value === 'true' ? 'true' : 'false';
  }

  return value;
}
