import { useEffect, useState } from 'react';
import {
  Sheet,
  SheetContent,
  SheetDescription,
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

  useEffect(() => {
    if (emulator && open) {
      setName(emulator.name);
      setProtocolId(emulator.protocolId);
      setTargetUrl(emulator.targetUrl);
      setIntervalSec(emulator.intervalSec);
      setError(null);
      setInitialSnapshot(
        JSON.stringify({
          name: emulator.name,
          protocolId: emulator.protocolId,
          targetUrl: emulator.targetUrl,
          intervalSec: emulator.intervalSec,
        })
      );
    }
  }, [emulator, open]);

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
      return setError(localization.routes.emulators.components.editEmulatorDrawer.text1);
    if (!Number.isFinite(protocolId) || protocolId < 1) {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.text2);
    }
    if (!targetUrl.trim())
      return setError(localization.routes.emulators.components.editEmulatorDrawer.text3);
    try {
      new URL(targetUrl);
    } catch {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.text4);
    }
    if (!Number.isFinite(intervalSec) || intervalSec < 1) {
      return setError(localization.routes.emulators.components.editEmulatorDrawer.text5);
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
              {localization.routes.emulators.components.editEmulatorDrawer.text6}
            </SheetTitle>
            {/* <SheetDescription>
              {emulator
                ? localization.routes.emulators.components.editEmulatorDrawer.text7(emulator.name)
                : ''}
            </SheetDescription> */}
          </SheetHeader>

          <div className="flex-1 space-y-5 overflow-y-auto p-6">
            <div className="space-y-2">
              <Label htmlFor="em-name">
                {localization.routes.emulators.components.editEmulatorDrawer.text8}
              </Label>
              <Input
                id="em-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="CNC_Mill_01"
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
                {localization.routes.emulators.components.editEmulatorDrawer.text9}
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="em-url">
                {localization.routes.emulators.components.editEmulatorDrawer.text10}
              </Label>
              <Input
                id="em-url"
                type="url"
                value={targetUrl}
                onChange={(e) => setTargetUrl(e.target.value)}
                placeholder="https://scada.local/api/ingest"
                className="font-mono text-xs"
              />
              <p className="text-xs text-muted-foreground">
                {localization.routes.emulators.components.editEmulatorDrawer.text11}
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="em-interval">
                {localization.routes.emulators.components.editEmulatorDrawer.text12}
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
                  <span>{localization.routes.emulators.components.editEmulatorDrawer.text13}</span>
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
              {localization.routes.emulators.components.editEmulatorDrawer.text14}
            </Button>
            <Button onClick={() => void handleSave()}>
              {localization.routes.emulators.components.editEmulatorDrawer.text15}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
      <AlertDialog open={confirmCloseOpen} onOpenChange={setConfirmCloseOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.editEmulatorDrawer.text16}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.editEmulatorDrawer.text17}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.editEmulatorDrawer.text18}
            </AlertDialogCancel>
            <AlertDialogAction onClick={closeWithoutSaving}>
              {localization.routes.emulators.components.editEmulatorDrawer.text19}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
