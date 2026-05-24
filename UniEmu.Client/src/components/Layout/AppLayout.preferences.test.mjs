import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const layoutDir = dirname(fileURLToPath(import.meta.url));
const srcDir = join(layoutDir, '..', '..');

test('app layout persists theme and sidebar preferences through config helpers', async () => {
  const [config, appLayout] = await Promise.all([
    readFile(join(srcDir, 'config', 'app-theme.ts'), 'utf8'),
    readFile(join(layoutDir, 'AppLayout.tsx'), 'utf8'),
  ]);

  assert.match(config, /appThemeStorageKey\s*=\s*'app-theme'/);
  assert.match(config, /appSidebarCollapsedStorageKey\s*=\s*'app-sidebar-collapsed'/);
  assert.match(config, /export function saveAppThemePreference/);
  assert.match(config, /export function getInitialSidebarCollapsed/);
  assert.match(config, /export function saveSidebarCollapsedPreference/);
  assert.match(config, /readLocalStorageItem\(appSidebarCollapsedStorageKey\)/);
  assert.match(config, /writeLocalStorageItem\(appSidebarCollapsedStorageKey,\s*String\(collapsed\)\)/);

  assert.match(appLayout, /getInitialSidebarCollapsed\(\)/);
  assert.match(appLayout, /saveAppThemePreference\(newTheme\)/);
  assert.match(appLayout, /saveSidebarCollapsedPreference\(newCollapsed\)/);
  assert.doesNotMatch(appLayout, /localStorage\.setItem\('app-theme'/);
});
