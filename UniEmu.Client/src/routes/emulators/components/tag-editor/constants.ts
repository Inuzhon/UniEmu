import { localization } from '@/localization';
import type { TagSource, TagType } from '@/types/uniemu';

export const TAG_EDITOR_SOURCES: { id: TagSource; label: string }[] = [
  { id: 'static', label: localization.routes.emulators.components.addTagDrawer.staticSourceLabel },
  { id: 'generator', label: localization.routes.emulators.components.addTagDrawer.generatorSourceLabel },
  { id: 'scenario', label: localization.routes.emulators.components.addTagDrawer.scenarioSourceLabel },
  {
    id: 'formulaScript',
    label: localization.routes.emulators.components.addTagDrawer.formulaScriptSourceLabel,
  },
  { id: 'script', label: localization.routes.emulators.components.addTagDrawer.scriptSourceLabel },
];

export const TAG_EDITOR_TYPES: TagType[] = ['int', 'double', 'string', 'bool'];

export const DEFAULT_INLINE_SCRIPT = 'return 0;\n';
export const TAG_IDENTITY_SEPARATOR = '\u0000';
