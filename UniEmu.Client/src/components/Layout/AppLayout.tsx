import { useState, useEffect } from 'react';
import { Outlet } from '@tanstack/react-router';
import { ChangelogDialog } from '@/components/ChangelogDialog';
import {
  applyDocumentAppTheme,
  getInitialAppTheme,
  getInitialSidebarCollapsed,
  saveAppThemePreference,
  saveSidebarCollapsedPreference,
  type AppTheme,
} from '@/config/app-theme';
import { useUniEmuStore } from '@/store/uniemu-store';
import { Sidebar } from './Sidebar';

export function AppLayout() {
  const [collapsed, setCollapsed] = useState(() => getInitialSidebarCollapsed());
  const [changelogOpen, setChangelogOpen] = useState(false);
  const [theme, setTheme] = useState<AppTheme>(() => getInitialAppTheme());
  const hydrate = useUniEmuStore((s) => s.hydrate);
  const connectRealtime = useUniEmuStore((s) => s.connectRealtime);

  useEffect(() => {
    const initialTheme = getInitialAppTheme();
    setTheme(initialTheme);
    applyDocumentAppTheme(initialTheme);
  }, []);

  useEffect(() => {
    void hydrate().then(() => connectRealtime());
  }, [connectRealtime, hydrate]);

  const handleThemeToggle = () => {
    const newTheme = theme === 'dark' ? 'light' : 'dark';
    setTheme(newTheme);
    saveAppThemePreference(newTheme);
    applyDocumentAppTheme(newTheme);
  };

  const handleSidebarToggle = () => {
    setCollapsed((currentCollapsed) => {
      const newCollapsed = !currentCollapsed;
      saveSidebarCollapsedPreference(newCollapsed);
      return newCollapsed;
    });
  };

  return (
    <div className="flex h-screen w-full overflow-hidden bg-background text-foreground">
      <Sidebar
        collapsed={collapsed}
        onToggle={handleSidebarToggle}
        onOpenChangelog={() => setChangelogOpen(true)}
        theme={theme}
        onThemeToggle={handleThemeToggle}
      />
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* <Header /> */}
        <main className="flex-1 overflow-y-auto bg-background">
          <Outlet />
        </main>
      </div>
      <ChangelogDialog open={changelogOpen} onOpenChange={setChangelogOpen} />
    </div>
  );
}
