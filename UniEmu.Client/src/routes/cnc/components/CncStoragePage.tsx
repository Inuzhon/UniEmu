import { useMemo, useRef, useState } from 'react';
import { Download, FileText, FolderOpen, Globe2, Upload } from 'lucide-react';
import { useUniEmuStore } from '@/store/uniemu-store';
import type { CncProgram, CncScope } from '@/types/uniemu';
import { Button } from '@/components/ui/button';
import { StorageEmptyHint } from '@/components/storage/StorageEmptyHint';
import { StorageExplorerLayout } from '@/components/storage/StorageExplorerLayout';
import { StorageFileRow } from '@/components/storage/StorageFileRow';
import { StorageTreeGroup } from '@/components/storage/StorageTreeGroup';
import { formatNumber } from '@/utils/format';
import { DropZone } from './DropZone';
import { CncViewer } from './CncViewer';
import { fmtSize, isTextByName } from '../utils/-index';
import { localization } from '@/localization';

interface UploadTarget {
  scope: CncScope;
  emulatorId?: string;
}

export function CncStoragePage() {
  const programs = useUniEmuStore((s) => s.cncPrograms);
  const emulators = useUniEmuStore((s) => s.emulators);
  const uploadCncProgram = useUniEmuStore((s) => s.uploadCncProgram);
  const updateCncProgram = useUniEmuStore((s) => s.updateCncProgram);
  const deleteCncProgram = useUniEmuStore((s) => s.deleteCncProgram);

  const [selectedId, setSelectedId] = useState<string | null>(programs[0]?.id ?? null);
  const [query, setQuery] = useState('');
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(() => ({
    shared: true,
    ...Object.fromEntries(emulators.map((e) => [e.id, true])),
  }));
  const [dragOver, setDragOver] = useState<string | null>(null);
  const [uploadTarget, setUploadTarget] = useState<UploadTarget | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const normalizedQuery = query.toLowerCase();

  const sharedPrograms = useMemo(
    () =>
      programs.filter(
        (p) => p.scope === 'shared' && p.name.toLowerCase().includes(normalizedQuery)
      ),
    [programs, normalizedQuery]
  );

  const programsByEmulator = useMemo(() => {
    const map: Record<string, CncProgram[]> = {};
    for (const e of emulators) map[e.id] = [];
    for (const p of programs) {
      if (
        p.scope === 'emulator' &&
        p.emulatorId &&
        p.name.toLowerCase().includes(normalizedQuery)
      ) {
        map[p.emulatorId]?.push(p);
      }
    }
    return map;
  }, [programs, emulators, normalizedQuery]);

  const selected = programs.find((p) => p.id === selectedId) ?? null;

  const totalCount = programs.length;
  const totalBytes = programs.reduce((s, p) => s + p.sizeBytes, 0);

  const toggleGroup = (key: string) => setOpenGroups((g) => ({ ...g, [key]: !g[key] }));

  const handleFiles = async (files: FileList | File[], target: UploadTarget) => {
    const list = Array.from(files);
    let lastId: string | null = null;
    for (const file of list) {
      const isText = isTextByName(file.name) || file.type.startsWith('text/');
      let content: string;
      if (isText) {
        try {
          content = await file.text();
        } catch {
          content = '';
        }
      } else {
        content = `[binary: ${file.name}]`;
      }
      lastId = await uploadCncProgram({
        name: file.name,
        scope: target.scope,
        emulatorId: target.emulatorId,
        content,
        sizeBytes: file.size,
        isBinary: !isText,
        description: '',
      });
    }
    if (lastId) setSelectedId(lastId);
  };

  const triggerUpload = (target: UploadTarget) => {
    setUploadTarget(target);
    setTimeout(() => fileInputRef.current?.click(), 0);
  };

  const handleDownload = (p: CncProgram) => {
    const blob = new Blob([p.content], {
      type: p.isBinary ? 'application/octet-stream' : 'text/plain;charset=utf-8',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = p.name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <input
        ref={fileInputRef}
        type="file"
        multiple
        className="hidden"
        onChange={(e) => {
          if (e.target.files && uploadTarget) {
            void handleFiles(e.target.files, uploadTarget);
            e.target.value = '';
          }
        }}
      />

      <StorageExplorerLayout
        title={localization.routes.cnc.components.cncStoragePage.title}
        description={localization.routes.cnc.components.cncStoragePage.description}
        totalValue={formatNumber(totalCount)}
        totalCaption={
          <>
            {localization.routes.cnc.components.cncStoragePage.filesCountSuffix}
            {fmtSize(totalBytes)}
          </>
        }
        action={
          <Button className="gap-2" onClick={() => triggerUpload({ scope: 'shared' })}>
            <Upload className="h-4 w-4" /> {localization.routes.cnc.components.cncStoragePage.uploadButtonLabel}
          </Button>
        }
        searchValue={query}
        searchPlaceholder={localization.routes.cnc.components.cncStoragePage.searchPlaceholder}
        onSearchChange={setQuery}
        sidebar={
          <>
            <StorageTreeGroup
              groupKey="shared"
              label={localization.routes.cnc.components.cncStoragePage.sharedProgramsTitle}
              count={sharedPrograms.length}
              icon={Globe2}
              open={openGroups.shared}
              onToggle={() => toggleGroup('shared')}
              accent="text-accent"
              addTitle={localization.routes.cnc.components.treeGroup.uploadToGroupTitle}
              dragActive={dragOver === 'shared'}
              onDragEnter={() => setDragOver('shared')}
              onDragLeave={() => setDragOver(null)}
              onDrop={(files) => {
                setDragOver(null);
                void handleFiles(files, { scope: 'shared' });
              }}
              onAdd={() => triggerUpload({ scope: 'shared' })}
            >
              {sharedPrograms.map((p) => (
                <StorageFileRow
                  key={p.id}
                  icon={FileText}
                  name={p.name}
                  active={p.id === selectedId}
                  onSelect={() => setSelectedId(p.id)}
                  onRename={(name) => void updateCncProgram(p.id, { name })}
                  deleteTitle={localization.routes.cnc.components.fileRow.deleteActionLabel}
                  confirmDeleteMessage={localization.routes.cnc.components.fileRow.confirmDeleteMessage(p.name)}
                  meta={
                    <span className="ml-auto shrink-0 font-mono text-[10px] text-muted-foreground/70">
                      {fmtSize(p.sizeBytes)}
                    </span>
                  }
                  actions={[
                    {
                      icon: Download,
                      title: localization.routes.cnc.components.fileRow.downloadActionLabel,
                      onClick: () => handleDownload(p),
                      className: 'hover:text-primary',
                    },
                  ]}
                  onDelete={() => {
                    void deleteCncProgram(p.id);
                    if (selectedId === p.id) setSelectedId(null);
                  }}
                />
              ))}
              {sharedPrograms.length === 0 && (
                <StorageEmptyHint label={localization.routes.cnc.components.cncStoragePage.sharedProgramsDropHint} />
              )}
            </StorageTreeGroup>

            <div className="my-2 mx-3 border-t border-border/60" />

            <div className="px-3 py-1 text-[10px] uppercase tracking-widest text-muted-foreground">
              {localization.routes.cnc.components.cncStoragePage.emulatorProgramsTitle}
            </div>

            {emulators.map((em) => {
              const list = programsByEmulator[em.id] ?? [];
              return (
                <StorageTreeGroup
                  key={em.id}
                  groupKey={em.id}
                  label={em.name}
                  count={list.length}
                  icon={FolderOpen}
                  open={openGroups[em.id]}
                  onToggle={() => toggleGroup(em.id)}
                  accent="text-primary"
                  addTitle={localization.routes.cnc.components.treeGroup.uploadToGroupTitle}
                  dragActive={dragOver === em.id}
                  onDragEnter={() => setDragOver(em.id)}
                  onDragLeave={() => setDragOver(null)}
                  onDrop={(files) => {
                    setDragOver(null);
                    void handleFiles(files, {
                      scope: 'emulator',
                      emulatorId: em.id,
                    });
                  }}
                  onAdd={() => triggerUpload({ scope: 'emulator', emulatorId: em.id })}
                >
                  {list.map((p) => (
                    <StorageFileRow
                      key={p.id}
                      icon={FileText}
                      name={p.name}
                      active={p.id === selectedId}
                      onSelect={() => setSelectedId(p.id)}
                      onRename={(name) => void updateCncProgram(p.id, { name })}
                      deleteTitle={localization.routes.cnc.components.fileRow.deleteActionLabel}
                      confirmDeleteMessage={localization.routes.cnc.components.fileRow.confirmDeleteMessage(
                        p.name
                      )}
                      meta={
                        <span className="ml-auto shrink-0 font-mono text-[10px] text-muted-foreground/70">
                          {fmtSize(p.sizeBytes)}
                        </span>
                      }
                      actions={[
                        {
                          icon: Download,
                          title: localization.routes.cnc.components.fileRow.downloadActionLabel,
                          onClick: () => handleDownload(p),
                          className: 'hover:text-primary',
                        },
                      ]}
                      onDelete={() => {
                        void deleteCncProgram(p.id);
                        if (selectedId === p.id) setSelectedId(null);
                      }}
                    />
                  ))}
                  {list.length === 0 && (
                    <StorageEmptyHint
                      label={localization.routes.cnc.components.cncStoragePage.emptyEmulatorProgramsHint}
                    />
                  )}
                </StorageTreeGroup>
              );
            })}
          </>
        }
      >
        {selected ? (
          <CncViewer
            key={selected.id}
            file={selected}
            emulatorName={
              selected.emulatorId
                ? emulators.find((e) => e.id === selected.emulatorId)?.name
                : undefined
            }
            onDownload={() => handleDownload(selected)}
            onSave={(patch) => void updateCncProgram(selected.id, patch)}
          />
        ) : (
          <DropZone
            onDrop={(files) => void handleFiles(files, { scope: 'shared' })}
            onPick={() => triggerUpload({ scope: 'shared' })}
          />
        )}
      </StorageExplorerLayout>
    </>
  );
}
