import { Construction } from 'lucide-react';
import { localization } from '@/localization';

interface PlaceholderProps {
  title: string;
  description: string;
}

export function PagePlaceholder({ title, description }: PlaceholderProps) {
  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        <p className="text-sm text-muted-foreground">{description}</p>
      </div>
      <div className="flex flex-col items-center justify-center gap-4 rounded-lg border border-dashed border-border bg-card/40 p-16 text-center">
        <div className="flex h-12 w-12 items-center justify-center rounded-full border border-signal-warning/40 bg-signal-warning/10">
          <Construction className="h-5 w-5 text-signal-warning" />
        </div>
        <div className="max-w-md">
          <p className="font-medium">{localization.components.pagePlaceholder.title}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {localization.components.pagePlaceholder.description}
          </p>
        </div>
      </div>
    </div>
  );
}
