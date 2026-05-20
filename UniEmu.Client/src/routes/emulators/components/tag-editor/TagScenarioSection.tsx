import { memo } from 'react';
import { localization } from '@/localization';
import type { TagScenarioConfig, TagType } from '@/types/uniemu';
import { ScenarioEditor } from '../tag-scenario/ScenarioEditor';

interface Props {
  scenario: TagScenarioConfig;
  tagType: TagType;
  onChange: (next: TagScenarioConfig) => void;
}

export const TagScenarioSection = memo(function TagScenarioSection({
  scenario,
  tagType,
  onChange,
}: Props) {
  return (
    <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
      <div className="flex items-baseline justify-between">
        <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          {localization.routes.emulators.components.addTagDrawer.scenarioTimelineTitle}
        </h4>
        <span className="text-[10px] text-muted-foreground">
          {localization.routes.emulators.components.addTagDrawer.scenarioTimelineTriggerHint}
        </span>
      </div>
      <ScenarioEditor value={scenario} onChange={onChange} tagType={tagType} />
    </section>
  );
});
