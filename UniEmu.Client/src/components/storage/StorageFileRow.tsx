import { Pencil, Trash2 } from 'lucide-react';
import type { ComponentType } from 'react';
import { useEffect, useState } from 'react';

export interface StorageFileRowAction {
  icon: ComponentType<{ className?: string }>;
  title: string;
  onClick: () => void;
  className?: string;
}

interface StorageFileRowProps {
  icon: ComponentType<{ className?: string }>;
  name: string;
  active: boolean;
  onSelect: () => void;
  onRename: (name: string) => void;
  onDelete: () => void;
  deleteTitle: string;
  confirmDeleteMessage?: string;
  meta?: React.ReactNode;
  actions?: StorageFileRowAction[];
}

export function StorageFileRow({
  icon: Icon,
  name,
  active,
  onSelect,
  onRename,
  onDelete,
  deleteTitle,
  confirmDeleteMessage,
  meta,
  actions = [],
}: StorageFileRowProps) {
  const [editingName, setEditingName] = useState(false);
  const [draftName, setDraftName] = useState(name);

  useEffect(() => {
    setDraftName(name);
  }, [name]);

  const cancelRename = () => {
    setDraftName(name);
    setEditingName(false);
  };

  const submitRename = () => {
    if (draftName.trim() && draftName.trim() !== name) {
      onRename(draftName.trim());
    }
    setEditingName(false);
  };

  const iconClassName = `h-3.5 w-3.5 shrink-0 ${active ? 'text-primary' : 'text-muted-foreground'}`;

  return (
    <div
      className={`group flex items-center gap-1 rounded-l px-2 py-1 transition-colors ${
        active
          ? 'bg-primary/15 text-primary'
          : 'text-muted-foreground hover:bg-muted/40 hover:text-foreground'
      }`}
    >
      {editingName ? (
        <div className="flex min-w-0 flex-1 items-center gap-2">
          <Icon className={iconClassName} />
          <input
            autoFocus
            value={draftName}
            spellCheck={false}
            onChange={(e) => setDraftName(e.target.value)}
            onBlur={submitRename}
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === 'Enter') submitRename();
              if (e.key === 'Escape') cancelRename();
            }}
            className="h-6 min-w-0 flex-1 rounded border border-border bg-background px-1.5 font-mono text-xs text-foreground outline-none focus:border-primary"
          />
        </div>
      ) : (
        <button
          onClick={onSelect}
          className="flex min-w-0 flex-1 items-center gap-2 truncate text-left"
        >
          <Icon className={iconClassName} />
          <span className="truncate">{name}</span>
          {meta}
        </button>
      )}
      {!editingName && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            setDraftName(name);
            setEditingName(true);
          }}
          className="rounded p-1 opacity-0 transition-opacity hover:text-primary group-hover:opacity-100"
          title="Rename"
        >
          <Pencil className="h-3 w-3" />
        </button>
      )}
      {actions.map((action) => {
        const ActionIcon = action.icon;
        return (
          <button
            key={action.title}
            onClick={(e) => {
              e.stopPropagation();
              action.onClick();
            }}
            className={`rounded p-1 opacity-0 transition-opacity group-hover:opacity-100 ${
              action.className ?? 'hover:text-primary'
            }`}
            title={action.title}
          >
            <ActionIcon className="h-3 w-3" />
          </button>
        );
      })}
      <button
        onClick={(e) => {
          e.stopPropagation();
          if (!confirmDeleteMessage || confirm(confirmDeleteMessage)) onDelete();
        }}
        className="rounded p-1 opacity-0 transition-opacity hover:text-signal-offline group-hover:opacity-100"
        title={deleteTitle}
      >
        <Trash2 className="h-3 w-3" />
      </button>
    </div>
  );
}
