import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useShallow } from 'zustand/react/shallow';
import { buildCsxDocumentUri } from '@/components/csx-language-client';
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
import {
  Sheet,
  SheetContent,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { localization } from '@/localization';
import { useUniEmuStore } from '@/store/uniemu-store';
import type {
  EmulatorTag,
  TagCalcConfig,
  TagScenarioConfig,
} from '@/types/uniemu';
import { InlineScriptEditorDrawer } from './tag-editor/InlineScriptEditorDrawer';
import { TagBasicsSection } from './tag-editor/TagBasicsSection';
import { TagCalcSection } from './tag-editor/TagCalcSection';
import { TagScenarioSection } from './tag-editor/TagScenarioSection';
import { TagScriptSection } from './tag-editor/TagScriptSection';
import { TagTriggerSection } from './tag-editor/TagTriggerSection';
import { DEFAULT_INLINE_SCRIPT, TAG_IDENTITY_SEPARATOR } from './tag-editor/constants';
import {
  buildTagFormSnapshot,
  buildTagPayload,
  createEmptyTagEditorFormState,
  createTagEditorFormState,
  hasScenarioDuration,
  normalizeTagIdentity,
} from './tag-editor/tagEditorUtils';
import { getTagValidationErrors, normalizeTagEditorForm } from './tag-editor/tagValidation';
import type { TagEditorFormState } from './tag-editor/types';

interface Props {
  emulatorId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tag?: EmulatorTag | null;
}

export function AddTagDrawer({ emulatorId, open, onOpenChange, tag }: Props) {
  const addTag = useUniEmuStore((s) => s.addTag);
  const updateTag = useUniEmuStore((s) => s.updateTag);
  const scripts = useUniEmuStore((s) => s.scripts);
  const cncPrograms = useUniEmuStore((s) => s.cncPrograms);
  const tagIdentityRows = useUniEmuStore(
    useShallow((s) =>
      (s.tagsByEmulator[emulatorId] ?? []).map((existingTag) =>
        [
          existingTag.id,
          normalizeTagIdentity(existingTag.name),
          normalizeTagIdentity(existingTag.key),
        ].join(TAG_IDENTITY_SEPARATOR),
      ),
    ),
  );

  const isEdit = !!tag;
  const initialSnapshotRef = useRef('');
  const [form, setForm] = useState<TagEditorFormState>(() =>
    normalizeTagEditorForm(createEmptyTagEditorFormState()),
  );
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editorDraft, setEditorDraft] = useState(DEFAULT_INLINE_SCRIPT);
  const [editorConfirmCloseOpen, setEditorConfirmCloseOpen] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;

    const nextForm = normalizeTagEditorForm(
      tag ? createTagEditorFormState(tag) : createEmptyTagEditorFormState(),
    );
    setForm(nextForm);
    setEditorDraft(nextForm.inlineScript);
    setConfirmCloseOpen(false);
    setEditorOpen(false);
    setEditorConfirmCloseOpen(false);
    setSubmitError(null);
    initialSnapshotRef.current = buildTagFormSnapshot(nextForm);
  }, [open, tag]);

  const setField = useCallback(<K extends keyof TagEditorFormState,>(
    field: K,
    value: TagEditorFormState[K],
  ) => {
    setSubmitError(null);
    setForm((current) =>
      Object.is(current[field], value)
        ? current
        : normalizeTagEditorForm({ ...current, [field]: value }),
    );
  }, []);

  const setCalc = useCallback((next: TagCalcConfig) => {
    setField('calc', next);
  }, [setField]);

  const setScenario = useCallback((next: TagScenarioConfig) => {
    setField('scenario', next);
  }, [setField]);

  const resetForm = useCallback(() => {
    const nextForm = normalizeTagEditorForm(createEmptyTagEditorFormState());
    setForm(nextForm);
    setEditorDraft(nextForm.inlineScript);
    setSubmitError(null);
    initialSnapshotRef.current = buildTagFormSnapshot(nextForm);
  }, []);

  const availableScripts = useMemo(
    () => scripts.filter((script) => script.scope === 'shared' || script.emulatorId === emulatorId),
    [scripts, emulatorId],
  );
  const visibleCncPrograms = useMemo(
    () =>
      cncPrograms
        .filter(
          (program) =>
            program.scope === 'shared' ||
            (program.scope === 'emulator' && program.emulatorId === emulatorId),
        )
        .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' })),
    [cncPrograms, emulatorId],
  );
  const sharedCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'shared'),
    [visibleCncPrograms],
  );
  const emulatorCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'emulator'),
    [visibleCncPrograms],
  );

  const duplicateNameError = useMemo(() => {
    const nextName = normalizeTagIdentity(form.name);
    if (!nextName) return null;

    return tagIdentityRows.some((existingTagIdentity) => {
      const [id, tagName] = existingTagIdentity.split(TAG_IDENTITY_SEPARATOR);
      return id !== tag?.id && tagName === nextName;
    })
      ? localization.routes.emulators.components.addTagDrawer.duplicateNameError
      : null;
  }, [tagIdentityRows, form.name, tag?.id]);

  const duplicateKeyError = useMemo(() => {
    const nextKey = normalizeTagIdentity(form.key);
    if (!nextKey) return null;

    return tagIdentityRows.some((existingTagIdentity) => {
      const [id, , tagKey] = existingTagIdentity.split(TAG_IDENTITY_SEPARATOR);
      return id !== tag?.id && tagKey === nextKey;
    })
      ? localization.routes.emulators.components.addTagDrawer.duplicateKeyError
      : null;
  }, [tagIdentityRows, form.key, tag?.id]);

  const isScenario = form.source === 'scenario';
  const validationErrors = useMemo(() => getTagValidationErrors(form), [form]);
  const showCalc =
    form.source === 'generator' || form.source === 'formula' || form.source === 'formulaScript';
  const showScript =
    form.source === 'formula' || form.source === 'script' || form.source === 'formulaScript';
  const showTrigger = !isScenario;
  const canSubmit = useMemo(
    () =>
      form.name.trim().length > 0 &&
      form.key.trim().length > 0 &&
      !duplicateNameError &&
      !duplicateKeyError &&
      validationErrors.length === 0 &&
      (!isScenario || hasScenarioDuration(form.scenario)),
    [duplicateKeyError, duplicateNameError, form.key, form.name, form.scenario, isScenario, validationErrors.length],
  );

  const hasDirtyChanges = useCallback(
    () =>
      open &&
      initialSnapshotRef.current.length > 0 &&
      buildTagFormSnapshot(form) !== initialSnapshotRef.current,
    [form, open],
  );

  const closeWithoutSaving = useCallback(() => {
    setConfirmCloseOpen(false);
    setEditorOpen(false);
    setEditorConfirmCloseOpen(false);
    resetForm();
    onOpenChange(false);
  }, [onOpenChange, resetForm]);

  const requestClose = useCallback(() => {
    if (hasDirtyChanges()) {
      setConfirmCloseOpen(true);
      return;
    }

    closeWithoutSaving();
  }, [closeWithoutSaving, hasDirtyChanges]);

  const handleOpenChange = useCallback((nextOpen: boolean) => {
    if (nextOpen) {
      onOpenChange(true);
      return;
    }

    requestClose();
  }, [onOpenChange, requestClose]);

  const handleSubmit = useCallback(async () => {
    if (!canSubmit) return;

    const payload = buildTagPayload(form);
    setSubmitError(null);
    try {
      if (isEdit && tag) {
        await updateTag(emulatorId, tag.id, payload);
      } else {
        await addTag(emulatorId, payload);
      }
      resetForm();
      onOpenChange(false);
    } catch (error) {
      const message = error instanceof Error
        ? error.message
        : localization.routes.emulators.components.addTagDrawer.saveTagError;
      setSubmitError(message);
    }
  }, [addTag, canSubmit, emulatorId, form, isEdit, onOpenChange, resetForm, tag, updateTag]);

  const inlineDocumentUri = useMemo(
    () =>
      buildCsxDocumentUri({
        id: tag?.id ?? 'new-inline',
        name: `inline/${tag?.id ?? 'new-tag'}.csx`,
        scope: 'emulator',
        emulatorId,
      }),
    [emulatorId, tag?.id],
  );

  const openEditor = useCallback(() => {
    setEditorDraft(form.inlineScript);
    setEditorOpen(true);
  }, [form.inlineScript]);

  const closeEditorWithoutSaving = useCallback(() => {
    setEditorConfirmCloseOpen(false);
    setEditorDraft(form.inlineScript);
    setEditorOpen(false);
  }, [form.inlineScript]);

  const requestEditorClose = useCallback(() => {
    if (editorOpen && editorDraft !== form.inlineScript) {
      setEditorConfirmCloseOpen(true);
      return;
    }

    closeEditorWithoutSaving();
  }, [closeEditorWithoutSaving, editorDraft, editorOpen, form.inlineScript]);

  const handleEditorOpenChange = useCallback((nextOpen: boolean) => {
    if (nextOpen) {
      setEditorOpen(true);
      return;
    }

    requestEditorClose();
  }, [requestEditorClose]);

  const applyEditorDraft = useCallback(() => {
    setField('inlineScript', editorDraft);
    setEditorOpen(false);
  }, [editorDraft, setField]);

  return (
    <>
      <Sheet open={open} onOpenChange={handleOpenChange}>
        <SheetContent
          onEscapeKeyDown={(event) => {
            if (isEdit) {
              event.preventDefault();
            }
          }}
          className={
            isScenario
              ? 'w-full overflow-y-auto sm:max-w-3xl lg:max-w-5xl xl:max-w-6xl'
              : 'w-full overflow-y-auto sm:max-w-lg'
          }
        >
          <SheetHeader>
            <SheetTitle>
              {isEdit
                ? localization.routes.emulators.components.addTagDrawer.editTitle
                : localization.routes.emulators.components.addTagDrawer.createTitle}
            </SheetTitle>
          </SheetHeader>

          <div className="space-y-5 py-6">
            <TagBasicsSection
              name={form.name}
              keyValue={form.key}
              specialParameter={form.specialParameter}
              type={form.type}
              source={form.source}
              staticValue={form.staticValue}
              description={form.description}
              enabled={form.enabled}
              roundEnabled={form.roundEnabled}
              roundDigits={form.roundDigits}
              duplicateNameError={duplicateNameError}
              duplicateKeyError={duplicateKeyError}
              sharedCncPrograms={sharedCncPrograms}
              emulatorCncPrograms={emulatorCncPrograms}
              visibleCncPrograms={visibleCncPrograms}
              onFieldChange={setField}
            />

            {isScenario && (
              <TagScenarioSection
                scenario={form.scenario}
                onChange={setScenario}
                tagType={form.type}
              />
            )}

            {showTrigger && (
              <TagTriggerSection
                triggerMode={form.triggerMode}
                triggerEvent={form.triggerEvent}
                cron={form.cron}
                intervalValue={form.intervalValue}
                intervalUnit={form.intervalUnit}
                onFieldChange={setField}
              />
            )}

            {showCalc && (
              <TagCalcSection
                calc={form.calc}
                tagType={form.type}
                onChange={setCalc}
              />
            )}

            {showScript && (
              <TagScriptSection
                availableScripts={availableScripts}
                scriptId={form.scriptId}
                inlineScript={form.inlineScript}
                onFieldChange={setField}
                onOpenEditor={openEditor}
              />
            )}

            {(validationErrors.length > 0 || submitError) && (
              <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
                {validationErrors.map((error) => (
                  <p key={error}>{error}</p>
                ))}
                {submitError && <p>{submitError}</p>}
              </div>
            )}
          </div>

          <SheetFooter className="gap-2">
            <Button variant="outline" onClick={requestClose}>
              {localization.routes.emulators.components.addTagDrawer.cancelButtonLabel}
            </Button>
            <Button onClick={() => void handleSubmit()} disabled={!canSubmit}>
              {isEdit
                ? localization.routes.emulators.components.addTagDrawer.saveButtonLabel
                : localization.routes.emulators.components.addTagDrawer.createTagButtonLabel}
            </Button>
          </SheetFooter>

          <InlineScriptEditorDrawer
            open={editorOpen}
            draft={editorDraft}
            documentUri={inlineDocumentUri}
            confirmCloseOpen={editorConfirmCloseOpen}
            onOpenChange={handleEditorOpenChange}
            onDraftChange={setEditorDraft}
            onApply={applyEditorDraft}
            onConfirmCloseOpenChange={setEditorConfirmCloseOpen}
            onCloseWithoutSaving={closeEditorWithoutSaving}
          />
        </SheetContent>
      </Sheet>
      <AlertDialog open={confirmCloseOpen} onOpenChange={setConfirmCloseOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {localization.routes.emulators.components.addTagDrawer.confirmCloseTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {localization.routes.emulators.components.addTagDrawer.confirmCloseDescription}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>
              {localization.routes.emulators.components.addTagDrawer.stayButtonLabel}
            </AlertDialogCancel>
            <AlertDialogAction onClick={closeWithoutSaving}>
              {localization.routes.emulators.components.addTagDrawer.closeButtonLabel}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
