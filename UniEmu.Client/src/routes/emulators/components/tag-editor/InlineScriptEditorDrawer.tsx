import { memo } from 'react';
import { MonacoCsxEditor } from '@/components/MonacoCsxEditor';
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
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { localization } from '@/localization';

interface Props {
  open: boolean;
  draft: string;
  documentUri: string;
  title: string;
  applyButtonLabel: string;
  confirmCloseOpen: boolean;
  error?: string | null;
  applying?: boolean;
  onOpenChange: (open: boolean) => void;
  onDraftChange: (value: string) => void;
  onApply: () => void;
  onConfirmCloseOpenChange: (open: boolean) => void;
  onCloseWithoutSaving: () => void;
}

export const InlineScriptEditorDrawer = memo(function InlineScriptEditorDrawer({
  open,
  draft,
  documentUri,
  title,
  applyButtonLabel,
  confirmCloseOpen,
  error,
  applying = false,
  onOpenChange,
  onDraftChange,
  onApply,
  onConfirmCloseOpenChange,
  onCloseWithoutSaving,
}: Props) {
  return (
    <>
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent
          side="right"
          onEscapeKeyDown={(event) => event.preventDefault()}
          onInteractOutside={(event) => event.preventDefault()}
          className="flex w-full flex-col gap-0 p-0 sm:max-w-3xl"
        >
          <SheetHeader className="border-b border-border px-6 py-4">
            <SheetTitle>{title}</SheetTitle>
          </SheetHeader>
          {error && (
            <div className="border-b border-destructive/40 bg-destructive/10 px-6 py-2 font-mono text-xs text-destructive">
              {error}
            </div>
          )}
          <div className="flex-1 overflow-hidden">
            <MonacoCsxEditor value={draft} onChange={onDraftChange} documentUri={documentUri} />
          </div>
          <SheetFooter className="border-t border-border px-6 py-4">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {localization.routes.emulators.components.addTagDrawer.editorCancelButtonLabel}
            </Button>
            <Button onClick={onApply} disabled={applying}>
              {applyButtonLabel}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
      <AlertDialog open={confirmCloseOpen} onOpenChange={onConfirmCloseOpenChange}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.addTagDrawer.confirmScriptEditorCloseTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {
                localization.routes.emulators.components.addTagDrawer
                  .confirmScriptEditorCloseDescription
              }
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.addTagDrawer.scriptEditorStayButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction onClick={onCloseWithoutSaving}>
              {localization.routes.emulators.components.addTagDrawer.scriptEditorCloseButtonLabel}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
});
