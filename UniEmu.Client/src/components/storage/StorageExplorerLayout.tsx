import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';

interface StorageExplorerLayoutProps {
  title: string;
  description: string;
  totalValue: React.ReactNode;
  totalCaption: React.ReactNode;
  action: React.ReactNode;
  searchValue: string;
  searchPlaceholder: string;
  onSearchChange: (value: string) => void;
  sidebar: React.ReactNode;
  children: React.ReactNode;
}

export function StorageExplorerLayout({
  title,
  description,
  totalValue,
  totalCaption,
  action,
  searchValue,
  searchPlaceholder,
  onSearchChange,
  sidebar,
  children,
}: StorageExplorerLayoutProps) {
  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card/40 px-6 py-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          <p className="text-sm text-muted-foreground">{description}</p>
        </div>
        <div className="flex items-center gap-4">
          <div className="hidden text-right md:block">
            <p className="font-mono text-sm font-semibold">{totalValue}</p>
            <p className="text-[10px] uppercase tracking-wider text-muted-foreground">
              {totalCaption}
            </p>
          </div>
          {action}
        </div>
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-[300px_1fr] divide-x divide-border">
        <aside className="flex min-h-0 min-w-0 flex-col bg-card/30">
          <div className="border-b border-border p-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchValue}
                onChange={(e) => onSearchChange(e.target.value)}
                placeholder={searchPlaceholder}
                className="h-8 pl-8 text-xs"
              />
            </div>
          </div>

          <div className="min-w-0 flex-1 overflow-y-auto overflow-x-hidden py-2 font-mono text-[13px]">
            {sidebar}
          </div>
        </aside>

        <section className="flex min-h-0 flex-col bg-background">{children}</section>
      </div>
    </div>
  );
}
