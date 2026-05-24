import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const storeDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src', 'store');
const srcDir = join(storeDir, '..');

test('store uses shared telemetry retention constants for defaults and clamping', async () => {
  const [constantsSource, storeSource, settingsSource] = await Promise.all([
    readFile(join(srcDir, 'lib', 'constants.ts'), 'utf8'),
    readFile(join(storeDir, 'uniemu-store.ts'), 'utf8'),
    readFile(join(srcDir, 'routes', 'settings', 'components', 'SettingsPage.tsx'), 'utf8'),
  ]);

  assert.match(constantsSource, /TELEMETRY_PACKET_RETENTION_LIMIT = 3000/);
  assert.match(constantsSource, /TELEMETRY_CHART_VISIBLE_PACKET_COUNT = 60/);
  assert.match(storeSource, /TELEMETRY_PACKET_RETENTION_LIMIT/);
  assert.match(storeSource, /packetRetention: TELEMETRY_PACKET_RETENTION_LIMIT/);
  assert.match(storeSource, /Math\.min\(TELEMETRY_PACKET_RETENTION_LIMIT, Math\.round\(n\)\)/);
  assert.doesNotMatch(storeSource, /packetRetention: 3000/);
  assert.doesNotMatch(storeSource, /Math\.min\(1000,/);
  assert.match(settingsSource, /TELEMETRY_PACKET_RETENTION_LIMIT/);
  assert.match(settingsSource, /max=\{TELEMETRY_PACKET_RETENTION_LIMIT\}/);
});
