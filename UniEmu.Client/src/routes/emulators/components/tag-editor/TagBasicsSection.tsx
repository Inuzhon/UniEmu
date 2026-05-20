import { memo, useMemo, useState } from 'react';
import { Check, ChevronsUpDown, FileText, Folder, Sparkles } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { localization } from '@/localization';
import { cn } from '@/lib/utils';
import { SPECIAL_PARAMETER_OPTIONS } from '@/types/uniemu';
import type { CncProgram, SpecialParameter, TagSource, TagType } from '@/types/uniemu';
import { getTagTypeLabel } from '../tag-scenario/calcLabels';
import { TAG_EDITOR_SOURCES, TAG_EDITOR_TYPES } from './constants';
import { clampRoundDigits, sanitizeStaticValue } from './tagEditorUtils';
import type { SetTagEditorField } from './types';

interface Props {
  name: string;
  keyValue: string;
  specialParameter: SpecialParameter;
  type: TagType;
  source: TagSource;
  staticValue: string;
  description: string;
  enabled: boolean;
  roundEnabled: boolean;
  roundDigits: number;
  duplicateNameError: string | null;
  duplicateKeyError: string | null;
  sharedCncPrograms: CncProgram[];
  emulatorCncPrograms: CncProgram[];
  visibleCncPrograms: CncProgram[];
  onFieldChange: SetTagEditorField;
}

export const TagBasicsSection = memo(function TagBasicsSection({
  name,
  keyValue,
  specialParameter,
  type,
  source,
  staticValue,
  description,
  enabled,
  roundEnabled,
  roundDigits,
  duplicateNameError,
  duplicateKeyError,
  sharedCncPrograms,
  emulatorCncPrograms,
  visibleCncPrograms,
  onFieldChange,
}: Props) {
  const [programPickerOpen, setProgramPickerOpen] = useState(false);
  const isProgramNameParameter = specialParameter === 'PrgName' || specialParameter === 'Subprogram';
  const selectedCncProgram = useMemo(
    () =>
      visibleCncPrograms.find(
        (program) =>
          program.name.localeCompare(staticValue, undefined, { sensitivity: 'accent' }) === 0,
      ),
    [staticValue, visibleCncPrograms],
  );

  const renderProgramOption = (program: CncProgram) => (
    <CommandItem
      key={program.id}
      value={`${program.scope}:${program.name}:${program.description}`}
      onSelect={() => {
        onFieldChange('staticValue', program.name);
        setProgramPickerOpen(false);
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
          staticValue === program.name ? 'opacity-100' : 'opacity-0',
        )}
      />
    </CommandItem>
  );

  const renderProgramNamePicker = () => (
    <Popover open={programPickerOpen} onOpenChange={setProgramPickerOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          className="h-9 w-full justify-between gap-2 font-mono text-xs"
        >
          <span className={cn('truncate', !staticValue && 'font-sans text-muted-foreground')}>
            {staticValue || localization.routes.emulators.components.addTagDrawer.programPickerSelectPlaceholder}
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
            {staticValue && !selectedCncProgram && (
              <CommandGroup
                heading={localization.routes.emulators.components.addTagDrawer.programPickerCurrentGroup}
              >
                <CommandItem
                  value={`current:${staticValue}`}
                  onSelect={() => setProgramPickerOpen(false)}
                  className="items-start gap-2 py-2"
                >
                  <FileText className="mt-0.5 h-3.5 w-3.5 text-muted-foreground" />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-mono text-xs">{staticValue}</div>
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
                {sharedCncPrograms.map((program) => renderProgramOption(program))}
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
                {emulatorCncPrograms.map((program) => renderProgramOption(program))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );

  const renderStaticValueInput = () => {
    if (type === 'string' && isProgramNameParameter) {
      return renderProgramNamePicker();
    }

    if (type === 'bool') {
      return (
        <div className="flex h-9 items-center rounded-md border border-border bg-background px-3">
          <Switch
            checked={staticValue === 'true'}
            onCheckedChange={(checked) => onFieldChange('staticValue', checked ? 'true' : 'false')}
          />
        </div>
      );
    }

    return (
      <Input
        value={staticValue}
        inputMode={type === 'int' ? 'numeric' : type === 'double' ? 'decimal' : undefined}
        spellCheck={type === 'string'}
        onChange={(event) => onFieldChange('staticValue', sanitizeStaticValue(type, event.target.value))}
        className="font-mono"
      />
    );
  };

  return (
    <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
      <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        {localization.routes.emulators.components.addTagDrawer.basicsSectionTitle}
      </h4>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.tagNameLabel}
          </Label>
          <Input
            value={name}
            onChange={(event) => onFieldChange('name', event.target.value)}
            placeholder={localization.routes.emulators.components.addTagDrawer.tagNamePlaceholder}
            className="font-mono"
          />
          <p className="text-[10px] text-muted-foreground">
            {localization.routes.emulators.components.addTagDrawer.tagNameHint}
          </p>
          {duplicateNameError && (
            <p className="text-[10px] text-destructive">{duplicateNameError}</p>
          )}
        </div>
        <div className="space-y-1.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.tagKeyLabel}
          </Label>
          <Input
            value={keyValue}
            spellCheck={false}
            onChange={(event) => onFieldChange('key', event.target.value)}
            placeholder={localization.routes.emulators.components.addTagDrawer.tagKeyPlaceholder}
            className="font-mono"
          />
          <p className="text-[10px] text-muted-foreground">
            {localization.routes.emulators.components.addTagDrawer.tagKeyHint}
          </p>
          {duplicateKeyError && (
            <p className="text-[10px] text-destructive">{duplicateKeyError}</p>
          )}
        </div>
      </div>

      <div className="space-y-1.5">
        <Label className="text-xs flex items-center gap-1.5">
          <Sparkles className="h-3 w-3 text-primary" />
          {localization.routes.emulators.components.addTagDrawer.specialParameterLabel}
          <span className="text-[10px] font-normal text-muted-foreground">
            {localization.routes.emulators.components.addTagDrawer.optionalBadge}
          </span>
        </Label>
        <Select
          value={specialParameter}
          onValueChange={(value) => onFieldChange('specialParameter', value as SpecialParameter)}
        >
          <SelectTrigger className="h-9">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {SPECIAL_PARAMETER_OPTIONS.map((option) => (
              <SelectItem key={option.value} value={option.value} className="text-xs">
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="space-y-1.5">
        <Label className="text-xs">
          {localization.routes.emulators.components.addTagDrawer.dataTypeLabel}
        </Label>
        <Select value={type} onValueChange={(value) => onFieldChange('type', value as TagType)}>
          <SelectTrigger className="h-9">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TAG_EDITOR_TYPES.map((tagType) => (
              <SelectItem key={tagType} value={tagType}>
                {getTagTypeLabel(tagType)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {type === 'double' && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-border bg-background/40 px-3 py-2">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.roundDigitsLabel}
          </Label>
          <div className="flex shrink-0 items-center gap-2">
            <Switch checked={roundEnabled} onCheckedChange={(checked) => onFieldChange('roundEnabled', checked)} />
            <Input
              type="number"
              min={0}
              max={15}
              step={1}
              value={roundDigits}
              disabled={!roundEnabled}
              onChange={(event) => onFieldChange('roundDigits', clampRoundDigits(Number(event.target.value) || 0))}
              className="h-8 w-20 font-mono"
            />
            <span className="text-xs text-muted-foreground">
              {localization.routes.emulators.components.addTagDrawer.digitsSuffixLabel}
            </span>
          </div>
        </div>
      )}

      <div className="space-y-1.5">
        <Label className="text-xs">
          {localization.routes.emulators.components.addTagDrawer.valueSourceLabel}
        </Label>
        <Select value={source} onValueChange={(value) => onFieldChange('source', value as TagSource)}>
          <SelectTrigger className="h-9">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TAG_EDITOR_SOURCES.map((sourceOption) => (
              <SelectItem key={sourceOption.id} value={sourceOption.id}>
                {sourceOption.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {source === 'static' && (
        <div className="space-y-1.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.staticValueLabel}
          </Label>
          {renderStaticValueInput()}
        </div>
      )}

      <div className="space-y-1.5">
        <Label className="text-xs">
          {localization.routes.emulators.components.addTagDrawer.descriptionLabel}
        </Label>
        <Textarea
          value={description}
          onChange={(event) => onFieldChange('description', event.target.value)}
          rows={2}
        />
      </div>

      <div className="flex items-start justify-between gap-3 rounded-md border border-border bg-background/40 p-2.5">
        <div className="space-y-0.5">
          <Label className="text-xs">
            {localization.routes.emulators.components.addTagDrawer.sendTagLabel}
          </Label>
          <p className="text-[10px] text-muted-foreground">
            {localization.routes.emulators.components.addTagDrawer.sendTagHint}
          </p>
        </div>
        <Switch checked={enabled} onCheckedChange={(checked) => onFieldChange('enabled', checked)} />
      </div>
    </section>
  );
});
