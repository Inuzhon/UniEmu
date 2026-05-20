import { memo } from 'react';
import { localization } from '@/localization';
import type { TagCalcConfig, TagType } from '@/types/uniemu';
import { CalcConfigFields } from '../tag-scenario/CalcConfigFields';
import { GENERATOR_CALC_TYPES } from '../tag-scenario/calcTypeOptions';

interface Props {
  calc: TagCalcConfig;
  tagType: TagType;
  onChange: (next: TagCalcConfig) => void;
}

export const TagCalcSection = memo(function TagCalcSection({ calc, tagType, onChange }: Props) {
  return (
    <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
      <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        {localization.routes.emulators.components.addTagDrawer.calcFormulaTitle}
      </h4>
      <CalcConfigFields
        value={calc}
        onChange={onChange}
        tagType={tagType}
        calcTypes={GENERATOR_CALC_TYPES}
      />
    </section>
  );
});
