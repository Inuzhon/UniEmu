import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const layoutDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'src', 'components', 'Layout');
const srcDir = join(layoutDir, '..', '..');
const clientDir = join(srcDir, '..');

test('app theme defaults and PWA colors come from shared config', async () => {
  const [config, appLayout, main, viteConfig, indexHtml, manifest] = await Promise.all([
    readFile(join(srcDir, 'config', 'app-theme.ts'), 'utf8'),
    readFile(join(layoutDir, 'AppLayout.tsx'), 'utf8'),
    readFile(join(srcDir, 'main.tsx'), 'utf8'),
    readFile(join(clientDir, 'vite.config.ts'), 'utf8'),
    readFile(join(clientDir, 'index.html'), 'utf8'),
    readFile(join(clientDir, 'public', 'manifest.webmanifest'), 'utf8'),
  ]);

  assert.match(config, /defaultTheme:\s*'light'/);
  assert.match(config, /light:\s*\{[\s\S]*themeColor:\s*'#f8fafc'/);
  assert.match(config, /dark:\s*\{[\s\S]*themeColor:\s*'#0b1220'/);
  assert.match(config, /function isAppTheme/);
  assert.match(config, /export function getInitialAppTheme/);
  assert.match(config, /export function applyDocumentAppTheme/);

  assert.match(appLayout, /import \{[\s\S]*applyDocumentAppTheme[\s\S]*getInitialAppTheme/);
  assert.match(appLayout, /getInitialAppTheme\(\)/);
  assert.match(appLayout, /applyDocumentAppTheme\(initialTheme\)/);
  assert.match(appLayout, /applyDocumentAppTheme\(newTheme\)/);
  assert.doesNotMatch(appLayout, /savedTheme \|\| 'dark'/);

  assert.match(main, /import \{ applyDocumentAppTheme, getInitialAppTheme \}/);
  assert.match(main, /applyDocumentAppTheme\(getInitialAppTheme\(\)\)/);
  assert.match(main, /applyDocumentAppTheme\(getInitialAppTheme\(\)\)[\s\S]*createRoot/);

  assert.match(viteConfig, /import \{ appThemeConfig/);
  assert.match(viteConfig, /appThemeConfig\.pwa\.light\.themeColor/);

  assert.match(indexHtml, /<meta name="theme-color" content="#f8fafc" \/>/);

  const manifestJson = JSON.parse(manifest);
  assert.equal(manifestJson.theme_color, '#f8fafc');
  assert.equal(manifestJson.background_color, '#f8fafc');
});
