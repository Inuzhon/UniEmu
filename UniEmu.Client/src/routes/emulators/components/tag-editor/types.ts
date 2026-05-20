import type {
  SpecialParameter,
  TagCalcConfig,
  TagIntervalUnit,
  TagScenarioConfig,
  TagSource,
  TagTriggerEvent,
  TagTriggerMode,
  TagType,
} from '@/types/uniemu';

export interface TagEditorFormState {
  key: string;
  specialParameter: SpecialParameter;
  name: string;
  type: TagType;
  source: TagSource;
  staticValue: string;
  description: string;
  enabled: boolean;
  roundEnabled: boolean;
  roundDigits: number;
  triggerMode: TagTriggerMode;
  triggerEvent: TagTriggerEvent;
  cron: string;
  intervalValue: number;
  intervalUnit: TagIntervalUnit;
  calc: TagCalcConfig;
  scriptId: string;
  inlineScript: string;
  scenario: TagScenarioConfig;
}

export type SetTagEditorField = <K extends keyof TagEditorFormState>(
  field: K,
  value: TagEditorFormState[K],
) => void;
