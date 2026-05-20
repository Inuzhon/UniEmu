import type {
  CalcType,
  ContinueOnFormulaEnd,
  TagIntervalUnit,
  TagSource,
  TagType,
} from '@/types/uniemu';

export const CALC_TYPE_LABELS: Record<CalcType, string> = {
  None: 'Без расчёта',
  Static: 'Статическое значение',
  Line: 'Линейная',
  Curve: 'Кривая',
  // Sequence: 'Последовательность',
  Random: 'Случайное значение',
  Sinusoid: 'Синусоида',
  Square: 'Прямоугольный сигнал',
  Sawtooth: 'Пилообразный сигнал',
  SquircleEarly: 'Плавный старт',
  SquircleLate: 'Плавное завершение',
};

export const TAG_TYPE_LABELS: Record<TagType, string> = {
  int: 'Целое число',
  double: 'Дробное число',
  string: 'Строка',
  bool: 'Логический',
};

export const TAG_SOURCE_LABELS: Record<TagSource, string> = {
  static: 'Статичное',
  formula: 'Формула',
  script: 'Скрипт',
  generator: 'Генератор',
  formulaScript: 'Формула + скрипт',
  cnc: 'ЧПУ',
  scenario: 'Сценарий',
};

export const TAG_INTERVAL_UNIT_LABELS: Record<TagIntervalUnit, string> = {
  ms: 'мс',
  sec: 'сек',
  min: 'мин',
};

export const CONTINUE_ON_FORMULA_END_LABELS: Record<ContinueOnFormulaEnd, string> = {
  NoSignal: 'Без сигнала',
  Zero: 'Обнулить',
  Repeat: 'Повторять',
  Stretch: 'Удерживать',
};

export const getCalcTypeLabel = (type: CalcType) => CALC_TYPE_LABELS[type] ?? type;

export const getTagTypeLabel = (type: TagType) => TAG_TYPE_LABELS[type] ?? type;

export const getTagSourceLabel = (source: TagSource) => TAG_SOURCE_LABELS[source] ?? source;

export const getTagIntervalUnitLabel = (unit: TagIntervalUnit) =>
  TAG_INTERVAL_UNIT_LABELS[unit] ?? unit;

export const getContinueOnFormulaEndLabel = (mode: ContinueOnFormulaEnd) =>
  CONTINUE_ON_FORMULA_END_LABELS[mode] ?? mode;
