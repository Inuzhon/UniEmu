import { useMemo, useState } from 'react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Check,
  ChevronsUpDown,
  FileText,
  Folder,
  Pencil,
  Plus,
  Trash2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import { cn } from '@/lib/utils';
import { localization } from '@/localization';
import type { CncProgram, EmulatorTag, TagSource, TagTrigger } from '@/types/uniemu';
import {
  getCalcTypeLabel,
  getContinueOnFormulaEndLabel,
  getTagIntervalUnitLabel,
  getTagSourceLabel,
  getTagTypeLabel,
} from './tag-scenario/calcLabels';
import { formatDuration, totalDuration } from './tag-scenario/scenarioMath';
import { ScenarioSparkline } from './tag-scenario/ScenarioPreviewChart';

type UpdateTag = (
  emulatorId: string,
  tagId: string,
  patch: Omit<EmulatorTag, 'id'>,
) => Promise<void>;

type EmulatorTagsTabProps = {
  emulatorId: string;
  tags: EmulatorTag[];
  visibleCncPrograms: CncProgram[];
  sharedCncPrograms: CncProgram[];
  emulatorCncPrograms: CncProgram[];
  updateTag: UpdateTag;
  deleteTag: (emulatorId: string, tagId: string) => Promise<void>;
  onAddTag: () => void;
  onEditTag: (tag: EmulatorTag) => void;
};

export function EmulatorTagsTab({
  emulatorId,
  tags,
  visibleCncPrograms,
  sharedCncPrograms,
  emulatorCncPrograms,
  updateTag,
  deleteTag,
  onAddTag,
  onEditTag,
}: EmulatorTagsTabProps) {
  const [programPreviewPickerOpenId, setProgramPreviewPickerOpenId] = useState<string | null>(null);
  const [deleteCandidateTag, setDeleteCandidateTag] = useState<EmulatorTag | null>(null);
  const enabledTags = useMemo(() => tags.filter((t) => t.enabled !== false), [tags]);
  const disabledTags = useMemo(() => tags.filter((t) => t.enabled === false), [tags]);

  const handleConfirmDelete = () => {
    if (!deleteCandidateTag) return;

    void deleteTag(emulatorId, deleteCandidateTag.id);
    setDeleteCandidateTag(null);
  };

  const renderProgramOption = (tag: EmulatorTag, program: CncProgram) => (
    <CommandItem
      key={program.id}
      value={`${program.scope}:${program.name}:${program.description}`}
      onSelect={() => {
        void updateTag(emulatorId, tag.id, { ...tag, preview: program.name });
        setProgramPreviewPickerOpenId(null);
      }}
      className="items-start gap-2 py-2"
    >
      <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <div className="truncate font-mono text-xs">{program.name}</div>
        {program.description && (
          <div className="truncate text-[10px] text-muted-foreground">
            {program.description}
          </div>
        )}
      </div>
      <Check
        className={cn(
          'mt-0.5 h-3.5 w-3.5',
          tag.preview === program.name ? 'opacity-100' : 'opacity-0'
        )}
      />
    </CommandItem>
  );

  const renderProgramPreviewPicker = (tag: EmulatorTag) => {
    const selectedCncProgram = visibleCncPrograms.find(
      (program) => program.name.localeCompare(tag.preview, undefined, { sensitivity: 'accent' }) === 0
    );

    return (
      <Popover
        open={programPreviewPickerOpenId === tag.id}
        onOpenChange={(open) => setProgramPreviewPickerOpenId(open ? tag.id : null)}
      >
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            className="h-7 w-full min-w-0 max-w-[18rem] justify-between gap-2 px-2 py-1 font-mono text-xs"
          >
            <span className={cn('truncate', !tag.preview && 'font-sans text-muted-foreground')}>
              {tag.preview || localization.routes.emulators.components.addTagDrawer.programPickerSelectPlaceholder}
            </span>
            <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 opacity-60" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-[--radix-popover-trigger-width] p-0">
          <Command>
            <CommandInput
              placeholder={localization.routes.emulators.components.addTagDrawer.programPickerSearchPlaceholder}
            />
            <CommandList
              onWheel={(event) => {
                event.preventDefault();
                event.stopPropagation();
                event.currentTarget.scrollTop += event.deltaY;
              }}
            >
              <CommandEmpty>
                {localization.routes.emulators.components.addTagDrawer.programPickerEmpty}
              </CommandEmpty>
              <CommandGroup>
                <CommandItem
                  value="clear-program-selection"
                  onSelect={() => {
                    void updateTag(emulatorId, tag.id, { ...tag, preview: '' });
                    setProgramPreviewPickerOpenId(null);
                  }}
                  className="items-center gap-2 py-2"
                >
                  <X className="h-3.5 w-3.5 text-muted-foreground" />
                  <span className="min-w-0 flex-1 truncate text-xs">
                    {localization.routes.emulators.components.addTagDrawer.programPickerClearSelection}
                  </span>
                  <Check
                    className={cn(
                      'h-3.5 w-3.5',
                      !tag.preview ? 'opacity-100' : 'opacity-0'
                    )}
                  />
                </CommandItem>
              </CommandGroup>
              {tag.preview && !selectedCncProgram && (
                <CommandGroup
                  heading={localization.routes.emulators.components.addTagDrawer.programPickerCurrentGroup}
                >
                  <CommandItem
                    value={`current:${tag.preview}`}
                    onSelect={() => setProgramPreviewPickerOpenId(null)}
                    className="items-start gap-2 py-2"
                  >
                    <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <div className="truncate font-mono text-xs">{tag.preview}</div>
                      <div className="truncate text-[10px] text-muted-foreground">
                        {localization.routes.emulators.components.addTagDrawer.programPickerCurrentMissing}
                      </div>
                    </div>
                    <Check className="mt-0.5 h-3.5 w-3.5" />
                  </CommandItem>
                </CommandGroup>
              )}
              {sharedCncPrograms.length > 0 && (
                <CommandGroup
                  heading={(
                    <span className="flex items-center gap-1.5">
                      <Folder className="h-3 w-3" />
                      {localization.routes.emulators.components.addTagDrawer.programPickerSharedGroup}
                    </span>
                  )}
                >
                  {sharedCncPrograms.map((program) => renderProgramOption(tag, program))}
                </CommandGroup>
              )}
              {emulatorCncPrograms.length > 0 && (
                <CommandGroup
                  heading={(
                    <span className="flex items-center gap-1.5">
                      <Folder className="h-3 w-3" />
                      {localization.routes.emulators.components.addTagDrawer.programPickerEmulatorGroup}
                    </span>
                  )}
                >
                  {emulatorCncPrograms.map((program) => renderProgramOption(tag, program))}
                </CommandGroup>
              )}
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    );
  };

  const renderRow = (tag: EmulatorTag) => (
    <tr
      key={tag.id}
      className="border-b border-border/60 transition-colors hover:bg-muted/20"
    >
      <td className="px-3 py-3 align-middle font-mono">
        <span title={tag.name} className="block max-w-full truncate">
          {tag.name}
        </span>
      </td>
      <td className="px-3 py-3 align-middle font-mono text-xs text-muted-foreground">
        <span title={tag.key === 'Custom' ? '-' : tag.key} className="block max-w-full truncate">
          {tag.key === 'Custom' ? '-' : tag.key}
        </span>
      </td>
      <td className="px-3 py-3 align-middle">
        <span className="inline-block rounded bg-muted px-2 py-0.5 font-mono text-[10px] uppercase leading-tight">
          {getTagTypeLabel(tag.type)}
        </span>
      </td>
      <td className="px-3 py-3 align-middle break-words text-xs text-muted-foreground [overflow-wrap:anywhere]">
        {getTagSourceLabel(tag.source)}
      </td>
      <td className="px-3 py-3 align-middle break-words text-xs text-muted-foreground [overflow-wrap:anywhere]">
        {formatTrigger(tag.trigger, tag.source)}
      </td>
      <td className="px-3 py-3 align-middle break-words font-mono text-[11px] text-muted-foreground [overflow-wrap:anywhere]">
        {tag.source === 'scenario' && tag.scenario
          ? localization.routes.emulators.components.emulatorDetailPage.scenarioSummary(
            tag.scenario.segments.length,
            formatDuration(totalDuration(tag.scenario)),
            getContinueOnFormulaEndLabel(tag.scenario.continueOnFormulaEnd ?? 'Repeat')
          )
          : getCalcTypeLabel(tag.calc?.type ?? 'None')}
      </td>
      <td className="px-3 py-3 align-middle font-mono text-xs text-primary">
        {tag.source === 'scenario' && tag.scenario ? (
          <ScenarioSparkline scenario={tag.scenario} tagType={tag.type} />
        ) : tag.source === 'static' ? (
          tag.type === 'bool' ? (
            <Switch
              checked={tag.preview === 'true'}
              onCheckedChange={(v) =>
                void updateTag(emulatorId, tag.id, { ...tag, preview: v ? 'true' : 'false' })
              }
            />
          ) : isProgramNameTag(tag) ? (
            renderProgramPreviewPicker(tag)
          ) : (
            <Input
              type="text"
              inputMode={
                tag.type === 'int' ? 'numeric' : tag.type === 'double' ? 'decimal' : undefined
              }
              spellCheck={tag.type === 'string'}
              defaultValue={tag.preview}
              onBlur={(e) => {
                const value = e.currentTarget.value;
                if (value !== tag.preview) void updateTag(emulatorId, tag.id, { ...tag, preview: value });
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') (e.currentTarget as HTMLInputElement).blur();
              }}
              className="h-7 w-full min-w-0 max-w-[18rem] px-2 py-1 font-mono text-xs"
            />
          )
        ) : (
          tag.preview
        )}
      </td>
      <td className="px-3 py-3 text-right align-middle">
        <div className="flex w-full items-center justify-end gap-2 whitespace-nowrap">
          <Switch
            checked={tag.enabled !== false}
            onCheckedChange={(v) => void updateTag(emulatorId, tag.id, { ...tag, enabled: v })}
            title={localization.routes.emulators.components.emulatorDetailPage.sendTagTitle}
          />
          <Button
            size="icon"
            variant="ghost"
            className="h-7 w-7 text-muted-foreground hover:text-foreground"
            onClick={() => onEditTag(tag)}
            title={localization.routes.emulators.components.emulatorDetailPage.editTagTitle}
          >
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="h-7 w-7 text-muted-foreground hover:text-signal-offline"
            onClick={() => setDeleteCandidateTag(tag)}
            title={localization.routes.emulators.components.emulatorDetailPage.deleteTagTitle}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      </td>
    </tr>
  );

  const renderTable = (rows: EmulatorTag[], emptyText: string, muted = false) => (
    <div className="overflow-x-auto">
      <table className={`w-full min-w-[62rem] table-fixed text-sm ${muted ? 'opacity-70' : ''}`}>
        <colgroup>
          <col className="w-[clamp(7rem,11vw,8.75rem)]" />
          <col className="w-[clamp(7rem,11vw,8.75rem)]" />
          <col className="w-[6rem]" />
          <col className="w-[6.5rem]" />
          <col className="w-[6.5rem]" />
          <col className="w-[clamp(10rem,18vw,12rem)]" />
          <col className="w-[clamp(10rem,18vw,12rem)]" />
          <col className="w-[8.75rem]" />
        </colgroup>
        <thead className="border-b border-border bg-muted/30 text-left text-[11px] uppercase tracking-wider text-muted-foreground">
          <tr>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.nameColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.keyColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.typeColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.sourceColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.triggerColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.calcColumnLabel}
            </th>
            <th className="px-3 py-2 font-medium">
              {localization.routes.emulators.components.emulatorDetailPage.previewColumnLabel}
            </th>
            <th className="px-3 py-2"></th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && (
            <tr>
              <td colSpan={8} className="px-3 py-8 text-center text-muted-foreground">
                {emptyText}
              </td>
            </tr>
          )}
          {rows.map(renderRow)}
        </tbody>
      </table>
    </div>
  );

  return (
    <>
      <div className="space-y-4">
        <div className="rounded-lg border border-border bg-card">
          <div className="flex items-center justify-between border-b border-border p-4">
            <div>
              <h3 className="font-semibold">
                {localization.routes.emulators.components.emulatorDetailPage.enabledTagsSectionTitle}
              </h3>
              <p className="text-xs text-muted-foreground">
                {localization.routes.emulators.components.emulatorDetailPage.enabledTagsSectionDescription}{' '}
                {localization.routes.emulators.components.emulatorDetailPage.tagsCountLabel(enabledTags.length)}
              </p>
            </div>
            <Button
              size="sm"
              onClick={onAddTag}
              title={localization.routes.emulators.components.emulatorDetailPage.addTagButtonLabel}
            >
              <Plus className="h-3.5 w-3.5" />{' '}
              {localization.routes.emulators.components.emulatorDetailPage.addTagButtonLabel}
            </Button>
          </div>
          {renderTable(
            enabledTags,
            localization.routes.emulators.components.emulatorDetailPage.emptyEnabledTagsMessage
          )}
        </div>

        {disabledTags.length > 0 && (
          <div className="rounded-lg border border-dashed border-border bg-card/50">
            <div className="border-b border-border/60 p-4">
              <h3 className="font-semibold text-muted-foreground">
                {localization.routes.emulators.components.emulatorDetailPage.disabledTagsSectionTitle}
              </h3>
              <p className="text-xs text-muted-foreground">
                {localization.routes.emulators.components.emulatorDetailPage.disabledTagsSectionDescription}{' '}
                {localization.routes.emulators.components.emulatorDetailPage.tagsCountLabel(disabledTags.length)}
              </p>
            </div>
            {renderTable(
              disabledTags,
              localization.routes.emulators.components.emulatorDetailPage.emptyDisabledTagsMessage,
              true
            )}
          </div>
        )}
      </div>

      <AlertDialog
        open={deleteCandidateTag !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteCandidateTag(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.emulatorDetailPage.deleteTagDialogTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {deleteCandidateTag
                ? localization.routes.emulators.components.emulatorDetailPage.deleteTagDialogDescription(
                  deleteCandidateTag.name
                )
                : ''}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.emulatorDetailPage.cancelButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="bg-signal-offline text-white hover:bg-signal-offline/90"
            >
              {localization.routes.emulators.components.emulatorDetailPage.confirmDeleteButtonLabel}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

function formatTrigger(trigger: TagTrigger, source?: TagSource): string {
  if (source === 'scenario')
    return localization.routes.emulators.components.emulatorDetailPage.timelineTriggerLabel;
  if (trigger.mode === 'once')
    return trigger.event === 'onStop'
      ? localization.routes.emulators.components.emulatorDetailPage.onStopTriggerLabel
      : localization.routes.emulators.components.emulatorDetailPage.onStartTriggerLabel;
  if (trigger.mode === 'cron')
    return localization.routes.emulators.components.emulatorDetailPage.scheduledTriggerLabel;
  return localization.routes.emulators.components.emulatorDetailPage.intervalTriggerLabel(
    trigger.intervalValue ?? 0,
    getTagIntervalUnitLabel(trigger.intervalUnit ?? 'sec')
  );
}

function isProgramNameTag(tag: EmulatorTag): boolean {
  return tag.source === 'static' &&
    tag.type === 'string' &&
    (tag.specialParameter === 'PrgName' || tag.specialParameter === 'Subprogram');
}
