import { useEffect, useMemo, useState } from 'react';
import { FileCode2, FolderOpen, Globe2, Plus } from 'lucide-react';
import { useUniEmuStore } from '@/store/uniemu-store';
import type { ScriptFile } from '@/types/uniemu';
import { Button } from '@/components/ui/button';
import { StorageEmptyHint } from '@/components/storage/StorageEmptyHint';
import { StorageExplorerLayout } from '@/components/storage/StorageExplorerLayout';
import { StorageFileRow } from '@/components/storage/StorageFileRow';
import { StorageTreeGroup } from '@/components/storage/StorageTreeGroup';
import { formatNumber } from '@/utils/format';
import { CreateScriptModal, NewScriptDraft } from './CreateScriptModal';
import { ScriptEditor } from './ScriptEditor';
import { localization } from '@/localization';

const UNSAVED_CHANGES_MESSAGE = 'Unsaved script changes will be lost. Continue?';

export function ScriptsPage() {
  const scripts = useUniEmuStore((s) => s.scripts);
  const emulators = useUniEmuStore((s) => s.emulators);
  const updateScript = useUniEmuStore((s) => s.updateScript);
  const createScript = useUniEmuStore((s) => s.createScript);
  const deleteScript = useUniEmuStore((s) => s.deleteScript);
  const renameScript = useUniEmuStore((s) => s.renameScript);

  const [selectedId, setSelectedId] = useState<string | null>(scripts[0]?.id ?? null);
  const [query, setQuery] = useState('');
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(() => ({
    shared: true,
    ...Object.fromEntries(emulators.map((e) => [e.id, true])),
  }));
  const [creating, setCreating] = useState<NewScriptDraft | null>(null);
  const [selectedDirty, setSelectedDirty] = useState(false);

  const sharedScripts = useMemo(
    () =>
      scripts
        .filter((s) => s.scope === 'shared')
        .filter((s) => s.name.toLowerCase().includes(query.toLowerCase())),
    [scripts, query]
  );

  const scriptsByEmulator = useMemo(() => {
    const map: Record<string, ScriptFile[]> = {};
    for (const e of emulators) map[e.id] = [];
    for (const sc of scripts) {
      if (sc.scope === 'emulator' && sc.emulatorId) {
        if (sc.name.toLowerCase().includes(query.toLowerCase())) {
          map[sc.emulatorId]?.push(sc);
        }
      }
    }
    return map;
  }, [scripts, emulators, query]);

  const selected = scripts.find((s) => s.id === selectedId) ?? null;

  useEffect(() => {
    if (!selectedDirty) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = UNSAVED_CHANGES_MESSAGE;
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [selectedDirty]);

  const toggleGroup = (key: string) => setOpenGroups((g) => ({ ...g, [key]: !g[key] }));

  const confirmDiscardUnsavedChanges = () =>
    !selectedDirty || confirm(UNSAVED_CHANGES_MESSAGE);

  const selectScript = (id: string) => {
    if (id === selectedId) return;
    if (!confirmDiscardUnsavedChanges()) return;

    setSelectedDirty(false);
    setSelectedId(id);
  };

  const beginCreate = (draft: NewScriptDraft) => {
    if (!confirmDiscardUnsavedChanges()) return;

    setCreating(draft);
  };

  const removeScript = (id: string) => {
    if (selectedId === id && !confirmDiscardUnsavedChanges()) return;

    void deleteScript(id);
    if (selectedId === id) {
      setSelectedDirty(false);
      setSelectedId(null);
    }
  };

  const totalCount = scripts.length;
  const totalBytes = scripts.reduce((sum, s) => sum + s.sizeBytes, 0);

  const handleCreate = async () => {
    if (!creating || !creating.name.trim()) return;
    const id = await createScript(creating);
    setSelectedId(id);
    setCreating(null);
  };

  return (
    <>
      <StorageExplorerLayout
        title={localization.routes.scripts.components.scriptsPage.text1}
        description={localization.routes.scripts.components.scriptsPage.text2}
        totalValue={formatNumber(totalCount)}
        totalCaption={
          <>
            {localization.routes.scripts.components.scriptsPage.text3}
            {(totalBytes / 1024).toFixed(1)}{' '}
            {localization.routes.scripts.components.scriptsPage.text4}
          </>
        }
        action={
          <Button className="gap-2" onClick={() => beginCreate({ name: '', scope: 'shared' })}>
            <Plus className="h-4 w-4" /> {localization.routes.scripts.components.scriptsPage.text5}
          </Button>
        }
        searchValue={query}
        searchPlaceholder={localization.routes.scripts.components.scriptsPage.text6}
        onSearchChange={setQuery}
        sidebar={
          <>
            <StorageTreeGroup
              label={localization.routes.scripts.components.scriptsPage.text7}
              count={sharedScripts.length}
              icon={Globe2}
              open={openGroups.shared}
              onToggle={() => toggleGroup('shared')}
              accent="text-accent"
              addTitle={localization.routes.scripts.components.treeGroup.text1}
              onAdd={() => beginCreate({ name: '', scope: 'shared' })}
            >
              {sharedScripts.map((sc) => (
                <StorageFileRow
                  key={sc.id}
                  icon={FileCode2}
                  name={sc.name}
                  active={sc.id === selectedId}
                  onSelect={() => selectScript(sc.id)}
                  onRename={(name) => void renameScript(sc.id, name)}
                  deleteTitle={localization.routes.scripts.components.fileRow.text2}
                  confirmDeleteMessage={localization.routes.scripts.components.fileRow.text1(
                    sc.name
                  )}
                  onDelete={() => removeScript(sc.id)}
                />
              ))}
              {sharedScripts.length === 0 && (
                <StorageEmptyHint
                  label={localization.routes.scripts.components.scriptsPage.text8}
                />
              )}
            </StorageTreeGroup>

            <div className="my-2 mx-3 border-t border-border/60" />

            <div className="px-3 py-1 text-[10px] uppercase tracking-widest text-muted-foreground">
              {localization.routes.scripts.components.scriptsPage.text9}
            </div>

            {emulators.map((em) => {
              const list = scriptsByEmulator[em.id] ?? [];
              return (
                <StorageTreeGroup
                  key={em.id}
                  label={em.name}
                  count={list.length}
                  icon={FolderOpen}
                  open={openGroups[em.id]}
                  onToggle={() => toggleGroup(em.id)}
                  accent="text-primary"
                  addTitle={localization.routes.scripts.components.treeGroup.text1}
                  onAdd={() => beginCreate({ name: '', scope: 'emulator', emulatorId: em.id })}
                >
                  {list.map((sc) => (
                    <StorageFileRow
                      key={sc.id}
                      icon={FileCode2}
                      name={sc.name}
                      active={sc.id === selectedId}
                      onSelect={() => selectScript(sc.id)}
                      onRename={(name) => void renameScript(sc.id, name)}
                      deleteTitle={localization.routes.scripts.components.fileRow.text2}
                      confirmDeleteMessage={localization.routes.scripts.components.fileRow.text1(
                        sc.name
                      )}
                      onDelete={() => removeScript(sc.id)}
                    />
                  ))}
                  {list.length === 0 && (
                    <StorageEmptyHint
                      label={localization.routes.scripts.components.scriptsPage.text10}
                    />
                  )}
                </StorageTreeGroup>
              );
            })}
          </>
        }
      >
        {selected ? (
          <ScriptEditor
            key={selected.id}
            file={selected}
            emulatorName={
              selected.emulatorId
                ? emulators.find((e) => e.id === selected.emulatorId)?.name
                : undefined
            }
            onSave={(content) => updateScript(selected.id, content)}
            onDirtyChange={setSelectedDirty}
          />
        ) : (
          <div className="flex flex-1 items-center justify-center">
            <div className="text-center">
              <FileCode2 className="mx-auto h-10 w-10 text-muted-foreground/40" />
              <p className="mt-3 text-sm text-muted-foreground">
                {localization.routes.scripts.components.scriptsPage.text11}
              </p>
            </div>
          </div>
        )}
      </StorageExplorerLayout>

      {/* Create dialog (lightweight inline modal) */}
      {creating && (
        <CreateScriptModal
          draft={creating}
          emulators={emulators}
          onChange={setCreating}
          onCancel={() => setCreating(null)}
          onSubmit={() => void handleCreate()}
        />
      )}
    </>
  );
}
