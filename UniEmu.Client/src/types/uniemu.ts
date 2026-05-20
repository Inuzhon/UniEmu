export type EmulatorStatus = 'Running' | 'Stopped' | 'Error' | 'Idle';

export interface Emulator {
  id: string;
  name: string;
  status: EmulatorStatus;
  protocolId: number;
  targetUrl: string;
  intervalSec: number;
  lastRun: string | null;
  nextRun: string | null;
  lastError: string | null;
  tagsCount: number;
  uptimeSec: number;
  totalRequests: number;
}

export type TagType = 'int' | 'double' | 'string' | 'bool';

export type TagSource =
  | 'static'
  | 'formula'
  | 'script'
  | 'generator'
  | 'formulaScript'
  | 'cnc'
  | 'scenario';

/**
 * Специализированные ключи параметров (UniEmu protocol).
 * "Custom" - пользовательский ключ, имя задаётся вручную.
 */
/**
 * Parameter Key — произвольная строка, которая отправляется в SCADA-эндпоинт.
 * Для удобства существует каталог известных специализированных параметров
 * (см. KEY_CATALOG в AddTagDrawer), но значение не ограничено им.
 */
export type TagKey = string;

export type CalcType =
  | 'None'
  | 'Static'
  | 'Line'
  | 'Curve'
  // | 'Sequence'
  | 'Random'
  | 'Sinusoid'
  | 'Square'
  | 'Sawtooth'
  | 'SquircleEarly'
  | 'SquircleLate';

/**
 * Режим срабатывания вычисления тега.
 *  - once:     один раз при выбранном событии (старт/стоп эмулятора)
 *  - cron:     по cron-выражению
 *  - interval: с заданной периодичностью (value + unit)
 */
export type TagTriggerMode = 'once' | 'cron' | 'interval';
export type TagTriggerEvent = 'onStart' | 'onStop';
export type TagIntervalUnit = 'ms' | 'sec' | 'min';

export interface TagTrigger {
  mode: TagTriggerMode;
  event?: TagTriggerEvent;        // для mode === "once"
  cron?: string;                  // для mode === "cron", напр. "0 0 * * *"
  intervalValue?: number;         // для mode === "interval"
  intervalUnit?: TagIntervalUnit; // для mode === "interval"
}

/** Параметры формулы расчёта значения. */
export interface TagCalcConfig {
  type: CalcType;
  start?: string;       // строковое представление (число/строка/JSON-массив для Sequence)
  finish?: string;
  duration?: number;    // сек
  amplitude?: number;
  period?: number;      // сек
  curvature?: number;
  distortion?: number;  // 0..100 (%)
}

/** Описание формульного источника (script | formula | formulaScript). */
export interface TagFormulaConfig {
  /** Подключённый существующий .csx-скрипт из общего хранилища. */
  scriptId?: string;
  /** Кастомный inline-скрипт, если scriptId не выбран. */
  inlineScript?: string;
}

/** Один сегмент таймлайн-сценария: формула + длительность. */
export interface TagScenarioSegment {
  id: string;
  duration: number;        // секунды
  calc: TagCalcConfig;
  label?: string;
}

/**
 * Поведение тега после завершения последнего сегмента сценария.
 *  - NoSignal: значение перестаёт публиковаться (null)
 *  - Zero:     удерживается значение 0
 *  - Repeat:   сценарий проигрывается заново (loop)
 *  - Stretch:  удерживается последнее значение
 */
export type ContinueOnFormulaEnd = 'NoSignal' | 'Zero' | 'Repeat' | 'Stretch';

/** Описание тега-сценария: упорядоченные сегменты + поведение по завершении. */
export interface TagScenarioConfig {
  segments: TagScenarioSegment[];
  continueOnFormulaEnd: ContinueOnFormulaEnd;
  /** Опциональное стартовое значение до первого сегмента. */
  startValue?: string;
}

/**
 * Специализированный параметр, не влияет на name/key. "None" = не задан.
 */
export type SpecialParameter =
  | 'None'
  | 'PrgName'
  | 'PartCounter'
  | 'ErrorNum'
  | 'FeedOvr'
  | 'SpindleOvr'
  | 'JogOvr'
  | 'FrameNum'
  | 'FrameText'
  | 'ToolNum'
  | 'WorkMode'
  | 'SystemState'
  | 'MachineReadiness'
  | 'TechnologicalStop'
  | 'EmergencyStop'
  | 'FeedRate'
  | 'ErrorText'
  | 'CycleTime'
  | 'SpindleSpeed'
  | 'SpindleLoad'
  | 'AxisLoad'
  | 'AxisPosition'
  | 'Message'
  | 'CNCModel'
  | 'FirmwareVersion'
  | 'SerialNumber'
  | 'PLCVersion'
  | 'Subprogram';

export const SPECIAL_PARAMETERS: SpecialParameter[] = [
  'None', 'PrgName', 'PartCounter', 'ErrorNum', 'FeedOvr', 'SpindleOvr',
  'JogOvr', 'FrameNum', 'FrameText', 'ToolNum', 'WorkMode', 'SystemState',
  'MachineReadiness', 'TechnologicalStop', 'EmergencyStop', 'FeedRate',
  'ErrorText', 'CycleTime', 'SpindleSpeed', 'SpindleLoad', 'AxisLoad',
  'AxisPosition', 'Message', 'CNCModel', 'FirmwareVersion', 'SerialNumber',
  'PLCVersion', 'Subprogram',
];

export const SPECIAL_PARAMETER_OPTIONS: { value: SpecialParameter; label: string }[] = [
  { value: 'None', label: 'Отсутствует' },
  { value: 'PrgName', label: 'Имя УП' },
  { value: 'FrameNum', label: 'Номер кадра УП' },
  { value: 'FrameText', label: 'Текст кадра УП' },
];

export interface EmulatorTag {
  id: string;
  name: string;
  key: TagKey;
  type: TagType;
  source: TagSource;
  preview: string;
  trigger: TagTrigger;
  calc?: TagCalcConfig;          // для source === "generator" | "formula" | "formulaScript"
  formula?: TagFormulaConfig;    // для source === "formula" | "script" | "formulaScript"
  scenario?: TagScenarioConfig;  // для source === "scenario"
  /** Специализированный параметр UniEmu protocol (отдельное поле). */
  specialParameter?: SpecialParameter;
  description?: string;
  /** Если false — тег не отправляется в SCADA-payload. По умолчанию true. */
  enabled?: boolean;
  roundDigits?: number | null;
}

export interface TelemetryPoint {
  timestamp: string;
  values: Record<string, unknown>;
}

export interface RuntimeTelemetryUpdate {
  emulatorId: string;
  point: TelemetryPoint;
}

export interface RuntimeTagValueUpdate {
  emulatorId: string;
  tagId: string;
  tagName: string;
  value: unknown;
  numericValue: number | null;
  timestamp: string;
}

export type EventLevel = 'info' | 'warn' | 'error' | 'success';

export interface SystemEvent {
  id: string;
  emulatorId: string;
  emulatorName: string;
  level: EventLevel;
  message: string;
  timestamp: string;
}

export type ScriptScope = 'shared' | 'emulator';

export type CncScope = 'shared' | 'emulator';

export interface CncProgram {
  id: string;
  name: string;            // file name (e.g. "PART_001.nc")
  scope: CncScope;
  emulatorId?: string;
  description: string;     // короткое описание УП
  content: string;         // текст G-кода
  sizeBytes: number;
  updatedAt: string;
  uploadedAt: string;
  isBinary?: boolean;      // если файл не текстовый — редактирование заблокировано
}

export interface ScriptFile {
  id: string;
  name: string;          // file name with .csx extension
  scope: ScriptScope;
  emulatorId?: string;   // set when scope === "emulator"
  content: string;
  updatedAt: string;
  sizeBytes: number;
}
