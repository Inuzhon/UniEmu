import { useState } from 'react';
import { Upload } from 'lucide-react';
import { localization } from '@/localization';
import { Button } from '@/components/ui/button';

export function DropZone({
  onDrop,
  onPick,
}: {
  onDrop: (files: FileList) => void;
  onPick: () => void;
}) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onDragEnter={(e) => {
        e.preventDefault();
        setHover(true);
      }}
      onDragOver={(e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
      }}
      onDragLeave={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node)) setHover(false);
      }}
      onDrop={(e) => {
        e.preventDefault();
        setHover(false);
        if (e.dataTransfer.files?.length) onDrop(e.dataTransfer.files);
      }}
      className="flex flex-1 items-center justify-center p-8"
    >
      <div
        className={`flex w-full max-w-md flex-col items-center justify-center rounded-xl border-2 border-dashed p-12 text-center transition-colors ${
          hover ? 'border-primary bg-primary/5 text-primary' : 'border-border text-muted-foreground'
        }`}
      >
        <Upload className="h-10 w-10" />
        <p className="mt-4 text-sm">
          {localization.routes.cnc.components.dropZone.text1}
          <br />
          {localization.routes.cnc.components.dropZone.text2}
        </p>
        <Button onClick={onPick} className="mt-3 gap-2">
          <Upload className="h-4 w-4" /> {localization.routes.cnc.components.dropZone.text3}
        </Button>
        <p className="mt-4 text-[11px] text-muted-foreground">
          {localization.routes.cnc.components.dropZone.text4}
        </p>
      </div>
    </div>
  );
}
