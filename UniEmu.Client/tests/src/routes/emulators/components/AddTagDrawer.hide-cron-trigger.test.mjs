import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const componentDir = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
  '..',
  '..',
  '..',
  'src',
  'routes',
  'emulators',
  'components'
);
const srcDir = join(componentDir, '..', '..', '..');

test('tag trigger editor hides cron from the frontend surface', async () => {
  const triggerSource = await readFile(
    join(componentDir, 'tag-editor/TagTriggerSection.tsx'),
    'utf8'
  );
  const drawerSource = await readFile(join(componentDir, 'AddTagDrawer.tsx'), 'utf8');
  const localizationSource = await readFile(join(srcDir, 'localization.ts'), 'utf8');
  const formTypesSource = await readFile(join(componentDir, 'tag-editor/types.ts'), 'utf8');
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');

  assert.doesNotMatch(triggerSource, /SelectItem value="cron"/);
  assert.doesNotMatch(triggerSource, /triggerModeCronLabel/);
  assert.doesNotMatch(triggerSource, /triggerMode === 'cron'/);
  assert.doesNotMatch(drawerSource, /cron=\{form\.cron\}/);
  assert.doesNotMatch(
    localizationSource,
    /triggerModeCronLabel|cronExpressionLabel|cronExamplePrefix|cronDailyMidnightHint/
  );
  assert.doesNotMatch(formTypesSource, /cron:\s*string/);
  assert.doesNotMatch(utilsSource, /cron:\s*'0 0 \* \* \*'/);
  assert.doesNotMatch(utilsSource, /cron:\s*tag\.trigger\.cron/);
  assert.doesNotMatch(utilsSource, /cron:\s*form\.cron/);
});

test('cron trigger values received by the editor are normalized to interval before saving', async () => {
  const validationSource = await readFile(
    join(componentDir, 'tag-editor/tagValidation.ts'),
    'utf8'
  );
  const utilsSource = await readFile(join(componentDir, 'tag-editor/tagEditorUtils.ts'), 'utf8');
  const tagsTabSource = await readFile(join(componentDir, 'EmulatorTagsTab.tsx'), 'utf8');
  const apiSource = await readFile(join(srcDir, 'api/uniemu-api.ts'), 'utf8');

  assert.match(validationSource, /triggerMode:\s*normalizeVisibleTriggerMode\(next\.triggerMode\)/);
  assert.doesNotMatch(validationSource, /Cron-выражение тега некорректно/);
  assert.match(utilsSource, /const triggerMode = normalizeVisibleTriggerMode\(form\.triggerMode\);/);
  assert.doesNotMatch(utilsSource, /form\.triggerMode === 'cron'/);
  assert.doesNotMatch(tagsTabSource, /cron:\s*\$\{/);
  assert.doesNotMatch(apiSource, /cron:\s*'CRON-/);
});
