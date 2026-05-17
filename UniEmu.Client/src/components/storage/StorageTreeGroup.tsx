import { ChevronDown, ChevronRight, Plus } from 'lucide-react';
import type { ComponentType, ReactNode } from 'react';

interface StorageTreeGroupProps {
  label: string;
  count: number;
  icon: ComponentType<{ className?: string }>;
  open: boolean;
  onToggle: () => void;
  onAdd: () => void;
  addTitle: string;
  accent: string;
  children: ReactNode;
  groupKey?: string;
  dragActive?: boolean;
  onDragEnter?: () => void;
  onDragLeave?: () => void;
  onDrop?: (files: FileList) => void;
}

export function StorageTreeGroup({
  label,
  count,
  icon: Icon,
  open,
  onToggle,
  onAdd,
  addTitle,
  accent,
  children,
  groupKey,
  dragActive = false,
  onDragEnter,
  onDragLeave,
  onDrop,
}: StorageTreeGroupProps) {
  const dragEnabled = Boolean(onDrop);

  return (
    <div
      onDragEnter={(e) => {
        if (!dragEnabled) return;
        e.preventDefault();
        onDragEnter?.();
      }}
      onDragOver={(e) => {
        if (!dragEnabled) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
      }}
      onDragLeave={(e) => {
        if (!dragEnabled) return;
        if (!e.currentTarget.contains(e.relatedTarget as Node)) onDragLeave?.();
      }}
      onDrop={(e) => {
        if (!dragEnabled) return;
        e.preventDefault();
        if (e.dataTransfer.files?.length) onDrop?.(e.dataTransfer.files);
      }}
      className={`mx-1 rounded transition-colors ${
        dragActive ? 'bg-primary/10 ring-1 ring-primary/40' : ''
      }`}
      data-group={groupKey}
    >
      <div className="group/row flex items-center gap-1 px-1 py-1">
        <button
          onClick={onToggle}
          className="flex flex-1 items-center gap-1.5 rounded px-1.5 py-1 text-left transition-colors hover:bg-muted/40"
        >
          {open ? (
            <ChevronDown className="h-3 w-3 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-3 w-3 text-muted-foreground" />
          )}
          <Icon className={`h-3.5 w-3.5 ${accent}`} />
          <span className="truncate text-foreground">{label}</span>
          <span className="ml-auto rounded bg-muted/60 px-1.5 py-px font-mono text-[10px] text-muted-foreground">
            {count}
          </span>
        </button>
        <button
          onClick={onAdd}
          className="rounded p-1 text-muted-foreground opacity-0 transition-opacity hover:bg-muted/60 hover:text-foreground group-hover/row:opacity-100"
          title={addTitle}
        >
          <Plus className="h-3 w-3" />
        </button>
      </div>
      {open && <div className="ml-4">{children}</div>}
    </div>
  );
}
