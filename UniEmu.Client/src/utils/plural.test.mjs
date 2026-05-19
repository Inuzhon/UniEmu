import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

const utilsDir = dirname(fileURLToPath(import.meta.url));
const srcDir = join(utilsDir, '..');

async function loadPluralModule() {
  const source = await readFile(join(utilsDir, 'plural.ts'), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });
  const context = {
    exports: {},
    Intl,
    module: { exports: {} },
  };

  context.module.exports = context.exports;
  vm.runInNewContext(outputText, context);

  return context.module.exports;
}

test('formatCount picks Russian plural forms for common frontend nouns', async () => {
  const { formatCount } = await loadPluralModule();

  assert.equal(formatCount(0, ['запись', 'записи', 'записей']), '0 записей');
  assert.equal(formatCount(1, ['запись', 'записи', 'записей']), '1 запись');
  assert.equal(formatCount(2, ['запись', 'записи', 'записей']), '2 записи');
  assert.equal(formatCount(5, ['запись', 'записи', 'записей']), '5 записей');
  assert.equal(formatCount(11, ['запись', 'записи', 'записей']), '11 записей');
  assert.equal(formatCount(21, ['запись', 'записи', 'записей']), '21 запись');
  assert.equal(formatCount(22, ['запись', 'записи', 'записей']), '22 записи');
  assert.equal(formatCount(25, ['запись', 'записи', 'записей']), '25 записей');

  assert.equal(formatCount(2, ['тег', 'тега', 'тегов']), '2 тега');
  assert.equal(formatCount(24, ['файл', 'файла', 'файлов']), '24 файла');
  assert.equal(formatCount(21, ['устройство', 'устройства', 'устройств']), '21 устройство');
  assert.equal(formatCount(3, ['строка', 'строки', 'строк']), '3 строки');
  assert.equal(formatCount(4, ['сегмент', 'сегмента', 'сегментов']), '4 сегмента');
  assert.equal(
    formatCount(22, ['последний пакет', 'последних пакета', 'последних пакетов']),
    '22 последних пакета'
  );
});

test('counted Russian labels are formatted through the shared plural helper', async () => {
  const [localization, dashboard, emulatorDetail, cncStorage, cncViewer, scripts, scenarioEditor] = await Promise.all([
    readFile(join(srcDir, 'localization.ts'), 'utf8'),
    readFile(join(srcDir, 'routes', 'components', 'DashboardPage.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'emulators', 'components', 'EmulatorDetailPage.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'cnc', 'components', 'CncStoragePage.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'cnc', 'components', 'CncViewer.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'scripts', 'components', 'ScriptsPage.tsx'), 'utf8'),
    readFile(join(srcDir, 'routes', 'emulators', 'components', 'tag-scenario', 'ScenarioEditor.tsx'), 'utf8'),
  ]);

  assert.match(localization, /formatCount/);
  assert.match(localization, /eventsCountLabel: \(p0: number\) => formatCount\(p0, \['запись', 'записи', 'записей'\]\)/);
  assert.match(localization, /tagsCountLabel: \(p0: number\) => formatCount\(p0, \['тег', 'тега', 'тегов'\]\)/);
  assert.match(localization, /filesCountLabel: \(p0: number\) => formatCount\(p0, \['файл', 'файла', 'файлов'\]\)/);
  assert.match(localization, /devicesCountLabel: \(p0: number\) => formatCount\(p0, \['устройство', 'устройства', 'устройств'\]\)/);
  assert.match(localization, /lineCountLabel: \(p0: number\) => formatCount\(p0, \['строка', 'строки', 'строк'\]\)/);
  assert.match(localization, /segmentsCountLabel: \(p0: number\) => formatCount\(p0, \['сегмент', 'сегмента', 'сегментов'\]\)/);

  assert.match(dashboard, /eventsCountLabel\(events\.length\)/);
  assert.match(dashboard, /activeCountLabel\(running\)/);
  assert.match(dashboard, /devicesCountLabel\(total\)/);
  assert.match(emulatorDetail, /eventsCountLabel\(events\.length\)/);
  assert.match(emulatorDetail, /tagsCountLabel\(enabledTags\.length\)/);
  assert.match(emulatorDetail, /tagsCountLabel\(enabledTagsForDispatcher\.length\)/);
  assert.match(emulatorDetail, /packetHistorySummary\(\s*packetRetention,\s*packets\.length\s*\)/);
  assert.match(cncStorage, /filesCountLabel\(totalCount\)/);
  assert.match(cncViewer, /lineCountLabel\(lineCount\)/);
  assert.match(scripts, /filesCountLabel\(totalCount\)/);
  assert.match(scenarioEditor, /segmentsCountLabel\(segments\.length\)/);

  for (const source of [dashboard, emulatorDetail, cncStorage, cncViewer, scripts, scenarioEditor]) {
    assert.doesNotMatch(source, /eventsCountSuffix|packetTagsSuffix|filesCountSuffix|lineCountSuffix|devicesCountSuffix|activeCountSuffix/);
    assert.doesNotMatch(source, /singleTagCountLabel|multipleTagsCountLabel|singleSegmentCountLabel|multipleSegmentsCountLabel/);
  }
});
