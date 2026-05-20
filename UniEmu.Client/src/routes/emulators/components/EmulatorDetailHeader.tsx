import { Link } from '@tanstack/react-router';
import { ArrowLeft, Download, PlayCircle, StopCircle, Trash2 } from 'lucide-react';
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
import { StatusBadge } from '@/components/StatusBadge';
import { localization } from '@/localization';
import type { Emulator } from '@/types/uniemu';

type EmulatorDetailHeaderProps = {
  emulator: Emulator;
  deleteOpen: boolean;
  templateDownloading: boolean;
  onDeleteOpenChange: (open: boolean) => void;
  onDelete: () => void;
  onDownloadTemplate: () => void;
  onToggleStatus: () => void;
};

export function EmulatorDetailHeader({
  emulator,
  deleteOpen,
  templateDownloading,
  onDeleteOpenChange,
  onDelete,
  onDownloadTemplate,
  onToggleStatus,
}: EmulatorDetailHeaderProps) {
  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="space-y-2">
          <Link
            to="/emulators"
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="h-3 w-3" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.backToListLabel}
          </Link>
          <div className="flex items-center gap-3">
            <h1 className="font-mono text-2xl font-semibold">{emulator.name}</h1>
            <StatusBadge status={emulator.status} />
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            size="sm"
            variant="outline"
            className="gap-2"
            disabled={templateDownloading}
            onClick={onDownloadTemplate}
            title={localization.routes.emulators.components.emulatorDetailPage.downloadDispatcherTemplate}
          >
            <Download className="h-3.5 w-3.5" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.downloadDispatcherTemplate}
          </Button>
          <Button
            size="sm"
            variant={emulator.status === 'Running' ? 'destructive' : 'default'}
            className="gap-2"
            onClick={onToggleStatus}
          >
            {emulator.status === 'Running' ? (
              <>
                <StopCircle className="h-3.5 w-3.5" />{' '}
                {localization.routes.emulators.components.emulatorDetailPage.stopButtonLabel}
              </>
            ) : (
              <>
                <PlayCircle className="h-3.5 w-3.5" />{' '}
                {localization.routes.emulators.components.emulatorDetailPage.startButtonLabel}
              </>
            )}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-2 border-signal-offline/40 text-signal-offline hover:bg-signal-offline/10 hover:text-signal-offline"
            onClick={() => onDeleteOpenChange(true)}
            title={localization.routes.emulators.components.emulatorDetailPage.deleteEmulatorButtonLabel}
          >
            <Trash2 className="h-3.5 w-3.5" />{' '}
            {localization.routes.emulators.components.emulatorDetailPage.deleteButtonLabel}
          </Button>
        </div>
      </div>

      <AlertDialog open={deleteOpen} onOpenChange={onDeleteOpenChange}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogSubjectLabel}
              <span className="font-mono text-foreground">{emulator.name}</span>{' '}
              {localization.routes.emulators.components.emulatorDetailPage.deleteDialogDescriptionSuffix}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.emulatorDetailPage.cancelButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={onDelete}
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
