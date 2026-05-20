import { useNavigate, useParams, useRouter } from '@tanstack/react-router';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useShallow } from 'zustand/react/shallow';
import { useUniEmuStore } from '@/store/uniemu-store';
import type { EmulatorTag } from '@/types/uniemu';
import { AddTagDrawer } from './AddTagDrawer';
import { EditEmulatorDrawer } from './EditEmulatorDrawer';
import { EmulatorDetailHeader } from './EmulatorDetailHeader';
import { EmulatorDetailTabs, type EmulatorDetailTabId } from './EmulatorDetailTabs';
import { EmulatorLogsTab } from './EmulatorLogsTab';
import { EmulatorMonitoringTab } from './EmulatorMonitoringTab';
import { EmulatorOverviewTab } from './EmulatorOverviewTab';
import { EmulatorTagsTab } from './EmulatorTagsTab';

const emptyTags: EmulatorTag[] = [];

export function EmulatorDetailPage() {
  const { id } = useParams({ from: '/emulators/$id' });
  const emulator = useUniEmuStore((s) => s.emulators.find((e) => e.id === id));
  const tags = useUniEmuStore((s) => s.tagsByEmulator[id] ?? emptyTags);
  const events = useUniEmuStore(useShallow((s) => s.events.filter((ev) => ev.emulatorId === id)));
  const cncPrograms = useUniEmuStore((s) => s.cncPrograms);
  const toggleStatus = useUniEmuStore((s) => s.toggleStatus);
  const downloadDispatcherTemplate = useUniEmuStore((s) => s.downloadDispatcherTemplate);
  const loadEmulatorDetails = useUniEmuStore((s) => s.loadEmulatorDetails);
  const subscribeRealtimeEmulator = useUniEmuStore((s) => s.subscribeRealtimeEmulator);
  const unsubscribeRealtimeEmulator = useUniEmuStore((s) => s.unsubscribeRealtimeEmulator);
  const deleteTag = useUniEmuStore((s) => s.deleteTag);
  const updateTag = useUniEmuStore((s) => s.updateTag);
  const deleteEmulator = useUniEmuStore((s) => s.deleteEmulator);
  const packetRetention = useUniEmuStore((s) => s.packetRetention);

  const [tab, setTab] = useState<EmulatorDetailTabId>('overview');
  const [addTagOpen, setAddTagOpen] = useState(false);
  const [editingTag, setEditingTag] = useState<EmulatorTag | null>(null);
  const [editConfigOpen, setEditConfigOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [templateDownloading, setTemplateDownloading] = useState(false);
  const navigate = useNavigate();
  const router = useRouter();

  useEffect(() => {
    void loadEmulatorDetails(id);
  }, [id, loadEmulatorDetails]);

  useEffect(() => {
    void subscribeRealtimeEmulator(id);
    return () => {
      void unsubscribeRealtimeEmulator(id);
    };
  }, [id, subscribeRealtimeEmulator, unsubscribeRealtimeEmulator]);

  const visibleCncPrograms = useMemo(
    () => [...cncPrograms
      .filter((program) =>
        program.scope === 'shared' ||
        (program.scope === 'emulator' && program.emulatorId === id))]
      .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' })),
    [cncPrograms, id]
  );
  const sharedCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'shared'),
    [visibleCncPrograms]
  );
  const emulatorCncPrograms = useMemo(
    () => visibleCncPrograms.filter((program) => program.scope === 'emulator'),
    [visibleCncPrograms]
  );

  const handleDelete = useCallback(async () => {
    await deleteEmulator(id);
    setDeleteOpen(false);
    if (router.history.canGoBack()) {
      router.history.back();
      return;
    }

    void navigate({ to: '/', replace: true });
  }, [deleteEmulator, id, navigate, router.history]);

  const handleDownloadDispatcherTemplate = useCallback(async () => {
    setTemplateDownloading(true);
    try {
      await downloadDispatcherTemplate(id);
    } finally {
      setTemplateDownloading(false);
    }
  }, [downloadDispatcherTemplate, id]);

  const handleAddTag = useCallback(() => {
    setEditingTag(null);
    setAddTagOpen(true);
  }, []);

  const handleEditTag = useCallback((tag: EmulatorTag) => {
    setEditingTag(tag);
    setAddTagOpen(true);
  }, []);

  if (!emulator) return null;

  return (
    <div className="space-y-4 p-6">
      <EmulatorDetailHeader
        emulator={emulator}
        deleteOpen={deleteOpen}
        templateDownloading={templateDownloading}
        onDeleteOpenChange={setDeleteOpen}
        onDelete={() => void handleDelete()}
        onDownloadTemplate={() => void handleDownloadDispatcherTemplate()}
        onToggleStatus={() => void toggleStatus(emulator.id)}
      />

      <EmulatorDetailTabs tab={tab} onTabChange={setTab} />

      {tab === 'overview' && (
        <EmulatorOverviewTab
          emulator={emulator}
          onEditConfig={() => setEditConfigOpen(true)}
        />
      )}

      {tab === 'tags' && (
        <EmulatorTagsTab
          emulatorId={id}
          tags={tags}
          visibleCncPrograms={visibleCncPrograms}
          sharedCncPrograms={sharedCncPrograms}
          emulatorCncPrograms={emulatorCncPrograms}
          updateTag={updateTag}
          deleteTag={deleteTag}
          onAddTag={handleAddTag}
          onEditTag={handleEditTag}
        />
      )}

      {tab === 'monitoring' && (
        <EmulatorMonitoringTab
          emulatorId={id}
          protocolId={emulator.protocolId}
          tags={tags}
          packetRetention={packetRetention}
        />
      )}

      {tab === 'logs' && <EmulatorLogsTab events={events} />}

      <AddTagDrawer
        emulatorId={id}
        open={addTagOpen}
        onOpenChange={(open) => {
          setAddTagOpen(open);
          if (!open) setEditingTag(null);
        }}
        tag={editingTag}
      />

      <EditEmulatorDrawer
        emulator={emulator}
        open={editConfigOpen}
        onOpenChange={setEditConfigOpen}
      />
    </div>
  );
}
