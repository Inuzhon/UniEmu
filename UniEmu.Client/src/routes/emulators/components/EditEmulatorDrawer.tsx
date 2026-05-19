import { useEffect, useRef, useState } from 'react';
import {
  Sheet,
  SheetContent,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { useUniEmuStore } from '@/store/uniemu-store';
import type { Emulator } from '@/types/uniemu';
import { localization } from '@/localization';

interface Props {
  emulator: Emulator | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function EditEmulatorDrawer({ emulator, open, onOpenChange }: Props) {
  const updateEmulator = useUniEmuStore((s) => s.updateEmulator);
  const [name, setName] = useState('');
  const [protocolId, setProtocolId] = useState(1);
  const [targetUrl, setTargetUrl] = useState('');
  const [intervalSec, setIntervalSec] = useState(5);
  const [error, setError] = useState<string | null>(null);
  const [initialSnapshot, setInitialSnapshot] = useState('');
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const emulatorRef = useRef<Emulator | null>(null);
  const editorSessionKey = open && emulator ? emulator.id : null;

  emulatorRef.current = emulator;

  useEffect(() => {
    const currentEmulator = emulatorRef.current;
    if (!currentEmulator || !editorSessionKey) return;

    setName(currentEmulator.name);
    setProtocolId(currentEmulator.protocolId);
    setTargetUrl(currentEmulator.targetUrl);
    setIntervalSec(currentEmulator.intervalSec);
    setError(null);
    setInitialSnapshot(
      JSON.stringify({
        name: currentEmulator.name,
        protocolId: currentEmulator.protocolId,
        targetUrl: currentEmulator.targetUrl,
        intervalSec: currentEmulator.intervalSec,
      })
    );
  }, [editorSessionKey]);

  const buildSnapshot = () =>
    JSON.stringify({
      name,
      protocolId,
      targetUrl,
      intervalSec,
    });

  const isDirty = open && initialSnapshot.length > 0 && buildSnapshot() !== initialSnapshot;

  const closeWithoutSaving = () => {
    setConfirmCloseOpen(false);
    onOpenChange(false);
  };

  const requestClose = () => {
    if (isDirty) {
      setConfirmCloseOpen(true);
      return;
    }

    closeWithoutSaving();
  };

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      onOpenChange(true);
      return;
    }

    requestClose();
  };

  const handleSave = async () => {
    if (!emulator) return;
    if (!name.trim())
      return setError(localization.routes.emulators.components.editEmulatorDrawer.nameRequiredMessage);
    if (!Number.isFinite(protocolId) || protocolId < 1) {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.protocolIdMinMessage);
    }
    if (!targetUrl.trim())
      return setError(localization.routes.emulators.components.editEmulatorDrawer.targetUrlRequiredMessage);
    try {
      new URL(targetUrl);
    } catch {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.targetUrlInvalidMessage);
    }
    if (!Number.isFinite(intervalSec) || intervalSec < 1) {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.intervalMinMessage);
    }
    await updateEmulator(emulator.id, {
      name: name.trim(),
      protocolId: Math.round(protocolId),
      targetUrl: targetUrl.trim(),
      intervalSec: Math.round(intervalSec),
    });
    onOpenChange(false);
  };

  return (
    <>
      <Sheet open={open} onOpenChange={handleOpenChange}>
        <SheetContent side="right" className="flex w-full flex-col gap-0 p-0 sm:max-w-md">
          <SheetHeader className="border-b border-border p-6">
            <SheetTitle>
              {localization.routes.emulators.components.editEmulatorDrawer.editTitle}
            </SheetTitle>
            {/* <SheetDescription>
              {emulator
                ? localization.routes.emulators.components.editEmulatorDrawer.emulatorTitle(emulator.name)
                : ''}
            </SheetDescription> */}
          </SheetHeader>

          <div className="flex-1 space-y-5 overflow-y-auto p-6">
            <div className="space-y-2">
              <Label htmlFor="em-name">
                {localization.routes.emulators.components.editEmulatorDrawer.nameLabel}
              </Label>
              <Input
                id="em-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={localization.routes.emulators.components.editEmulatorDrawer.namePlaceholder}
                className="font-mono"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="em-protocol-id">
                {localization.routes.emulators.components.editEmulatorDrawer.protocolIdLabel}
              </Label>
              <Input
                id="em-protocol-id"
                type="number"
                min={1}
                step={1}
                value={protocolId}
                onChange={(e) => setProtocolId(Number(e.target.value))}
                className="font-mono"
              />
              <p className="text-xs text-muted-foreground">
                {localization.routes.emulators.components.editEmulatorDrawer.protocolIdHint}
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="em-url">
                {localization.routes.emulators.components.editEmulatorDrawer.targetUrlLabel}
              </Label>
              <Input
                id="em-url"
                type="url"
                value={targetUrl}
                onChange={(e) => setTargetUrl(e.target.value)}
                placeholder={localization.routes.emulators.components.editEmulatorDrawer.targetUrlPlaceholder}
                className="font-mono text-xs"
              />
              <p className="text-xs text-muted-foreground">
                {localization.routes.emulators.components.editEmulatorDrawer.targetUrlHint}
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="em-interval">
                {localization.routes.emulators.components.editEmulatorDrawer.sendIntervalLabel}
              </Label>
              <Input
                id="em-interval"
                type="number"
                min={1}
                step={1}
                value={intervalSec}
                onChange={(e) => setIntervalSec(Number(e.target.value))}
                className="font-mono"
              />
            </div>

            {emulator && (
              <div className="rounded-md border border-border bg-muted/30 p-3 text-xs text-muted-foreground">
                <div className="flex justify-between">
                  <span>ID</span>
                  <span className="font-mono">{emulator.id}</span>
                </div>
                <div className="mt-1 flex justify-between">
                  <span>{localization.routes.emulators.components.editEmulatorDrawer.tagsCountLabel}</span>
                  <span className="font-mono">{emulator.tagsCount}</span>
                </div>
              </div>
            )}

            {error && (
              <p className="rounded-md border border-signal-offline/40 bg-signal-offline/10 p-2 text-xs text-signal-offline">
                {error}
              </p>
            )}
          </div>

          <SheetFooter className="border-t border-border p-4">
            <Button variant="outline" onClick={requestClose}>
              {localization.routes.emulators.components.editEmulatorDrawer.cancelButtonLabel}
            </Button>
            <Button onClick={() => void handleSave()}>
              {localization.routes.emulators.components.editEmulatorDrawer.saveButtonLabel}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
      <AlertDialog open={confirmCloseOpen} onOpenChange={setConfirmCloseOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.editEmulatorDrawer.confirmCloseTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.editEmulatorDrawer.confirmCloseDescription}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.editEmulatorDrawer.stayButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction onClick={closeWithoutSaving}>
              {localization.routes.emulators.components.editEmulatorDrawer.closeButtonLabel}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
