import { localization } from "@/localization";
import { RefreshCw, Settings } from "lucide-react";
import { Breadcrumbs } from "./Breadcrumbs";
import { Button } from "../ui/button";

export function Header() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-card/40 px-6 backdrop-blur">
      <Breadcrumbs />
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" className="gap-2">
          <RefreshCw className="h-3.5 w-3.5" />
          <span className="text-xs">{localization.components.layout.appLayout.text26}</span>
        </Button>
        <Button variant="outline" size="sm" className="gap-2">
          <Settings className="h-3.5 w-3.5" />
          <span className="text-xs">{localization.components.layout.appLayout.text27}</span>
        </Button>
      </div>
    </header>
  );
}
