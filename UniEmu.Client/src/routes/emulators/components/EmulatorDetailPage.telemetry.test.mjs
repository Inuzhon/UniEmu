import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('monitoring chart is built from all numeric telemetry values instead of fixed tags', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const source = [
    await readFile(join(componentDir, 'EmulatorMonitoringTab.tsx'), 'utf8'),
    await readFile(join(componentDir, 'emulator-detail/telemetry.ts'), 'utf8'),
  ].join('\n');

  assert.match(source, /const telemetryKeys = useMemo/);
  assert.match(source, /Object\.entries\(p\.values \?\? \{\}\)/);
  assert.match(source, /telemetryKeys\.map\(\(key\) =>/);
  assert.doesNotMatch(source, /dataKey="Temperature"/);
  assert.doesNotMatch(source, /dataKey="SpindleRPM"/);
  assert.doesNotMatch(source, /dataKey="FeedRate"/);
});

test('monitoring values and packet history use actual telemetry packet values', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const source = [
    await readFile(join(componentDir, 'EmulatorMonitoringTab.tsx'), 'utf8'),
    await readFile(join(componentDir, 'emulator-detail/telemetry.ts'), 'utf8'),
  ].join('\n');

  assert.match(source, /const activePoint = telemetryPoints\[activeIdx\]/);
  assert.match(source, /formatTelemetryValue\(activePoint\?\.values\?\.\\?\[t\.name\\?\]\)/);
  assert.match(source, /enabledTagsForDispatcher\.reduce<Record<string, string>>/);
  assert.match(source, /formatTelemetryValue\(p\.values\?\.\\?\[t\.name\\?\]\)/);
  assert.match(source, /const packets = useMemo/);
  assert.match(source, /function buildPacketHistory/);
  assert.match(source, /const arr = telemetryPoints\.map/);
  assert.doesNotMatch(source, /tagSeries\[t\.id\]\?\.\[activeIdx\]/);
  assert.doesNotMatch(source, /tagSeries\[t\.id\]\?\.\[i\]/);
});

test('packet history preview uses dispatcher monitoring JSON shape without useInnerId', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /MachineIntegrationId: protocolId/);
  assert.match(source, /ListValues: enabledTagsForDispatcher\.map\(\(t\) => \(\{/);
  assert.match(source, /Key: t\.key/);
  assert.match(source, /Value: pkt\.values\[t\.name\]/);
  assert.doesNotMatch(source, /UseInnerId/);
  assert.doesNotMatch(source, /tags: enabledTagsForDispatcher\.map/);
});

test('monitoring chart only plots int and double tags while packets keep all values', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const source = [
    await readFile(join(componentDir, 'EmulatorMonitoringTab.tsx'), 'utf8'),
    await readFile(join(componentDir, 'emulator-detail/telemetry.ts'), 'utf8'),
  ].join('\n');

  assert.match(source, /const numericTelemetryTags = useMemo/);
  assert.match(source, /t\.type === 'int' \|\| t\.type === 'double'/);
  assert.match(source, /parseTelemetryNumber\(p\.values\?\.\\?\[t\.name\\?\]\)/);
  assert.match(source, /function formatTelemetryValue\(value: unknown\): string/);
  assert.match(source, /if \(typeof value === 'string'\) return value;/);
});

test('telemetry detail page uses REST for initial history and realtime for subsequent updates', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorDetailPage.tsx'), 'utf8');

  assert.match(source, /void loadEmulatorDetails\(id\)/);
  assert.match(source, /void subscribeRealtimeEmulator\(id\)/);
  assert.match(source, /void unsubscribeRealtimeEmulator\(id\)/);
  assert.doesNotMatch(source, /refreshTelemetry/);
  assert.doesNotMatch(source, /setInterval/);
});

test('monitoring subscribes to telemetry state so realtime points rerender chart and packets', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const source = [
    await readFile(join(componentDir, 'EmulatorMonitoringTab.tsx'), 'utf8'),
    await readFile(join(componentDir, 'emulator-detail/telemetry.ts'), 'utf8'),
  ].join('\n');

  assert.match(source, /const liveTelemetry = useUniEmuStore\(\(s\) => s\.telemetryByEmulator\[id\] \?\? emptyTelemetry\)/);
  assert.match(source, /const visibleTelemetry = telemetryPaused \? pausedTelemetrySnapshot : liveTelemetry/);
  assert.match(source, /TELEMETRY_CHART_VISIBLE_PACKET_COUNT/);
  assert.match(source, /visibleTelemetry\.slice\(-TELEMETRY_CHART_VISIBLE_PACKET_COUNT\)/);
  assert.doesNotMatch(source, /slice\(-60\)/);
  assert.match(source, /telemetryPoints\.map/);
  assert.doesNotMatch(source, /const getTelemetry = useUniEmuStore/);
  assert.doesNotMatch(source, /getTelemetry\(id, 60\)/);
});

test('monitoring has a pause toggle that freezes and resumes visible telemetry', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /const \[telemetryPaused, setTelemetryPaused\] = useState\(false\)/);
  assert.match(source, /const \[pausedTelemetrySnapshot, setPausedTelemetrySnapshot\]/);
  assert.match(source, /handleTelemetryPauseToggle/);
  assert.match(source, /setPausedTelemetrySnapshot\(liveTelemetry\)/);
  assert.match(source, /setTelemetryPaused\(\(paused\) => !paused\)/);
  assert.match(source, /telemetryPaused \? .*emulatorDetailPage\.resume.* : .*emulatorDetailPage\.pause/s);
});

test('monitoring pause toggle is embedded into the telemetry chart header', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.doesNotMatch(source, /<div className="flex items-center justify-end">\s*<Button[\s\S]*?handleTelemetryPauseToggle/);
  assert.match(
    source,
    /<div className="mb-4 flex flex-wrap items-start justify-between gap-3">[\s\S]*?handleTelemetryPauseToggle/
  );
});

test('monitoring recharts tooltip follows the active theme colors', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /background: 'var\(--popover\)'/);
  assert.match(source, /border: '1px solid var\(--border\)'/);
  assert.match(source, /color: 'var\(--popover-foreground\)'/);
  assert.match(source, /labelStyle=\{\{ color: 'var\(--popover-foreground\)' \}\}/);
  assert.match(source, /itemStyle=\{\{ color: 'var\(--popover-foreground\)' \}\}/);
  assert.doesNotMatch(source, /background: 'oklch\(0\.22 0\.018 240\)'/);
});

test('monitoring recharts lines do not animate on telemetry updates', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(
    source,
    /<Line[\s\S]*?dataKey=\{key\}[\s\S]*?dot=\{false\}[\s\S]*?isAnimationActive=\{false\}/
  );
});

test('monitoring chart renders straight line segments without smoothing', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /<Line[\s\S]*?type="linear"[\s\S]*?dataKey=\{key\}/);
  assert.doesNotMatch(source, /type="monotone"/);
});

test('monitoring chart title is localized to Russian', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const source = await readFile(join(componentDir, 'EmulatorMonitoringTab.tsx'), 'utf8');
  const localization = await readFile(join(componentDir, '../../../localization.ts'), 'utf8');

  assert.match(source, /emulatorDetailPage\.telemetryTimeSeriesTitle/);
  assert.match(localization, /telemetryTimeSeriesTitle: 'Телеметрия - временной ряд'/);
  assert.doesNotMatch(source, /Telemetry â€” Time Series/);
  assert.doesNotMatch(source, /Telemetry — Time Series/);
});

test('monitoring chart lets users choose visible numeric tags locally', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /const \[hiddenTelemetryTagNames, setHiddenTelemetryTagNames\] = useState<Set<string>>/);
  assert.match(source, /const visibleNumericTelemetryTags = useMemo/);
  assert.match(source, /!hiddenTelemetryTagNames\.has\(t\.name\)/);
  assert.match(source, /setHiddenTelemetryTagNames\(\(current\) =>/);
  assert.match(source, /checked=\{!hiddenTelemetryTagNames\.has\(t\.name\)\}/);
  assert.match(source, /telemetryKeys\.map\(\(key\) => \{/);
});

test('monitoring chart keeps tag colors stable when visible tags are toggled', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorMonitoringTab.tsx'), 'utf8');

  assert.match(source, /const tagIndex = numericTelemetryTags\.findIndex\(\(t\) => t\.name === key\)/);
  assert.doesNotMatch(source, /const tagIndex = visibleNumericTelemetryTags\.findIndex\(\(t\) => t\.name === key\)/);
});
