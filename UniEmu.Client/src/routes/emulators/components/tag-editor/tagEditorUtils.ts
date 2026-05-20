import { localization } from '@/localization';
import type {
  EmulatorTag,
  TagCalcConfig,
  TagFormulaConfig,
  TagScenarioConfig,
  TagTrigger,
  TagType,
} from '@/types/uniemu';
import { DEFAULT_INLINE_SCRIPT } from './constants';
import type { TagEditorFormState } from './types';

export const sanitizeStaticValue = (type: TagType, value: string) => {
  if (type === 'int') {
    return value
      .replace(/[^\d-]/g, '')
      .replace(/(?!^)-/g, '');
  }

  if (type === 'double') {
    const normalized = value
      .replace(',', '.')
      .replace(/[^\d.-]/g, '')
      .replace(/(?!^)-/g, '');
    const [whole, ...fractionParts] = normalized.split('.');

    return fractionParts.length === 0 ? whole : `${whole}.${fractionParts.join('')}`;
  }

  return value;
};

export const normalizeTagIdentity = (value: string) => value.trim().toLocaleLowerCase('ru-RU');

export const clampRoundDigits = (value: number) =>
  Math.max(0, Math.min(15, Math.round(value)));

export const createDefaultScenario = (): TagScenarioConfig => ({
  segments: [],
  continueOnFormulaEnd: 'Repeat',
});

export const createDefaultCalc = (): TagCalcConfig => ({
  type: 'Line',
  start: '0',
  finish: '100',
  duration: 60,
  amplitude: 1,
  period: 10,
  curvature: 2,
  distortion: 0,
});

export const createEmptyTagEditorFormState = (): TagEditorFormState => ({
  key: '',
  specialParameter: 'None',
  name: '',
  type: 'int',
  source: 'static',
  staticValue: '',
  description: '',
  enabled: true,
  roundEnabled: false,
  roundDigits: 2,
  triggerMode: 'interval',
  triggerEvent: 'onStart',
  cron: '0 0 * * *',
  intervalValue: 1,
  intervalUnit: 'sec',
  calc: createDefaultCalc(),
  scriptId: '',
  inlineScript: DEFAULT_INLINE_SCRIPT,
  scenario: createDefaultScenario(),
});

export const createTagEditorFormState = (tag: EmulatorTag): TagEditorFormState => {
  const defaultCalc = createDefaultCalc();
  const calc = tag.calc
    ? {
        ...defaultCalc,
        ...tag.calc,
      }
    : defaultCalc;
  const nextScenario = tag.scenario ?? createDefaultScenario();

  return {
    key: tag.key,
    specialParameter: tag.specialParameter ?? 'None',
    name: tag.name,
    type: tag.type,
    source: tag.source,
    staticValue: tag.source === 'static' ? tag.preview : '',
    description: tag.description ?? '',
    enabled: tag.enabled ?? true,
    roundEnabled: tag.type === 'double' && tag.roundDigits !== null && tag.roundDigits !== undefined,
    roundDigits: tag.roundDigits ?? 2,
    triggerMode: tag.trigger.mode,
    triggerEvent: tag.trigger.event ?? 'onStart',
    cron: tag.trigger.cron ?? '0 0 * * *',
    intervalValue: tag.trigger.intervalValue ?? 1,
    intervalUnit: tag.trigger.intervalUnit ?? 'sec',
    calc,
    scriptId: tag.formula?.scriptId ?? '',
    inlineScript: tag.formula?.inlineScript ?? DEFAULT_INLINE_SCRIPT,
    scenario: nextScenario,
  };
};

export const buildTagFormSnapshot = (form: TagEditorFormState) =>
  JSON.stringify({
    key: form.key,
    specialParameter: form.specialParameter,
    name: form.name,
    type: form.type,
    source: form.source,
    staticValue: form.staticValue,
    description: form.description,
    enabled: form.enabled,
    roundDigits:
      form.type === 'double' && form.roundEnabled ? clampRoundDigits(form.roundDigits) : null,
    triggerMode: form.triggerMode,
    triggerEvent: form.triggerEvent,
    cron: form.cron,
    intervalValue: form.intervalValue,
    intervalUnit: form.intervalUnit,
    calc: form.calc,
    scriptId: form.scriptId,
    inlineScript: form.inlineScript,
    scenario: form.scenario,
  });

export const hasScenarioDuration = (scenario: TagScenarioConfig) =>
  scenario.segments.some((segment) => segment.duration > 0);

export const buildTagPayload = (form: TagEditorFormState): Omit<EmulatorTag, 'id'> => {
  const isScenario = form.source === 'scenario';
  const trigger: TagTrigger = isScenario
    ? { mode: 'interval', intervalValue: 1, intervalUnit: 'sec' }
    : { mode: form.triggerMode };

  if (!isScenario) {
    if (form.triggerMode === 'once') trigger.event = form.triggerEvent;
    if (form.triggerMode === 'cron') trigger.cron = form.cron;
    if (form.triggerMode === 'interval') {
      trigger.intervalValue = form.intervalValue;
      trigger.intervalUnit = form.intervalUnit;
    }
  }

  let calc: TagCalcConfig | undefined;
  if (form.source === 'generator' || form.source === 'formula' || form.source === 'formulaScript') {
    calc = form.calc;
  }

  let formula: TagFormulaConfig | undefined;
  if (form.source === 'formula' || form.source === 'script' || form.source === 'formulaScript') {
    formula = form.scriptId ? { scriptId: form.scriptId } : { inlineScript: form.inlineScript };
  }

  const preview =
    form.source === 'static'
      ? form.staticValue
      : form.source === 'cnc'
        ? localization.routes.emulators.components.addTagDrawer.cncPreviewLabel
        : isScenario
          ? localization.routes.emulators.components.addTagDrawer.scenarioPreviewLabel
          : calc?.type === 'Static'
            ? calc.start ?? ''
            : localization.routes.emulators.components.addTagDrawer.computedPreviewLabel;
  const normalizedPreview =
    form.source === 'static' && form.type === 'bool'
      ? form.staticValue === 'true' ? 'true' : 'false'
      : preview;

  return {
    name: form.name.trim(),
    key: form.key,
    type: form.type,
    source: form.source,
    preview: normalizedPreview,
    trigger,
    calc,
    formula,
    scenario: isScenario ? form.scenario : undefined,
    specialParameter: form.specialParameter !== 'None' ? form.specialParameter : undefined,
    description: form.description.trim() || undefined,
    enabled: form.enabled,
    roundDigits:
      form.type === 'double' && form.roundEnabled ? clampRoundDigits(form.roundDigits) : null,
  };
};
