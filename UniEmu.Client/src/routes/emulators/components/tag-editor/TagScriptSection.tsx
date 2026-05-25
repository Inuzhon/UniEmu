import { memo, useCallback } from 'react';
import { Pencil } from 'lucide-react';
import { MonacoCsxEditor } from '@/components/MonacoCsxEditor';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { localization } from '@/localization';
import type { ScriptFile } from '@/types/uniemu';
import type { SetTagEditorField } from './types';

interface Props {
  availableScripts: ScriptFile[];
  selectedScript?: ScriptFile | null;
  scriptId: string;
  inlineScript: string;
  onFieldChange: SetTagEditorField;
  onOpenEditor: () => void;
  onOpenStorageScriptEditor: () => void;
}

export const TagScriptSection = memo(function TagScriptSection({
  availableScripts,
  selectedScript,
  scriptId,
  inlineScript,
  onFieldChange,
  onOpenEditor,
  onOpenStorageScriptEditor,
}: Props) {
  const handlePreviewChange = useCallback(() => {}, []);
  const useInlineScript = !scriptId;

  return (
    <section className="space-y-3 rounded-md border border-border bg-muted/20 p-3">
      <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        {localization.routes.emulators.components.addTagDrawer.scriptSectionTitle}
      </h4>
      <div className="space-y-1.5">
        <Label className="text-xs">
          {localization.routes.emulators.components.addTagDrawer.existingScriptLabel}
        </Label>
        <Select
          value={scriptId || '__inline__'}
          onValueChange={(value) => onFieldChange('scriptId', value === '__inline__' ? '' : value)}
        >
          <SelectTrigger className="h-9">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__inline__">
              {localization.routes.emulators.components.addTagDrawer.customScriptDividerLabel}
            </SelectItem>
            {availableScripts.map((script) => (
              <SelectItem key={script.id} value={script.id} className="font-mono text-xs">
                {script.scope === 'shared' ? 'shared' : 'local'} - {script.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {selectedScript && (
        <div className="flex justify-end">
          <Button
            size="sm"
            variant="outline"
            className="h-7 gap-1.5 text-xs"
            onClick={onOpenStorageScriptEditor}
          >
            <Pencil className="h-3 w-3" />{' '}
            {localization.routes.emulators.components.addTagDrawer.storageScriptEditButtonLabel}
          </Button>
        </div>
      )}

      {useInlineScript && (
        <div className="space-y-1.5">
          <div className="flex items-center justify-between">
            <Label className="text-xs">
              {localization.routes.emulators.components.addTagDrawer.inlineScriptLabel}
            </Label>
            <Button
              size="sm"
              variant="outline"
              className="h-7 gap-1.5 text-xs"
              onClick={onOpenEditor}
            >
              <Pencil className="h-3 w-3" />{' '}
              {localization.routes.emulators.components.addTagDrawer.editScriptButtonLabel}
            </Button>
          </div>
          <div className="h-44 overflow-hidden rounded-md border border-border">
            <MonacoCsxEditor
              value={inlineScript}
              onChange={handlePreviewChange}
              minimap={false}
              readOnly
            />
          </div>
        </div>
      )}
    </section>
  );
});
