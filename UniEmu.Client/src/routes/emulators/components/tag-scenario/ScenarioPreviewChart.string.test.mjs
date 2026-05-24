import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('scenario preview components render string timelines without numeric sparkline labels', async () => {
  const componentDir = dirname(fileURLToPath(import.meta.url));
  const chartSource = await readFile(join(componentDir, 'ScenarioPreviewChart.tsx'), 'utf8');
  const editorSource = await readFile(join(componentDir, 'ScenarioEditor.tsx'), 'utf8');
  const tagsSource = await readFile(join(componentDir, '../EmulatorTagsTab.tsx'), 'utf8');

  assert.match(chartSource, /tagType\?: TagType/);
  assert.match(chartSource, /tagType === 'string'/);
  assert.match(chartSource, /function ScenarioStringTimeline/);
  assert.match(chartSource, /function ScenarioStringSparkline/);
  assert.match(chartSource, /title=\{tooltip\}/);
  assert.match(editorSource, /<ScenarioPreviewChart[\s\S]*tagType=\{tagType\}/);
  assert.match(editorSource, /<ScenarioSparkline[\s\S]*tagType=\{tagType\}/);
  assert.match(tagsSource, /<ScenarioSparkline scenario=\{tag\.scenario\} tagType=\{tag\.type\} \/>/);
});
