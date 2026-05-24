import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag table asks for confirmation before deleting a tag', async () => {
  const source = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'EmulatorTagsTab.tsx'), 'utf8');

  assert.match(source, /AlertDialog/);
  assert.match(source, /const \[deleteCandidateTag, setDeleteCandidateTag\] = useState<EmulatorTag \| null>\(null\)/);
  assert.match(source, /onClick=\{\(\) => setDeleteCandidateTag\(tag\)\}/);
  assert.match(source, /<AlertDialog\s+open=\{deleteCandidateTag !== null\}/);
  assert.match(source, /void deleteTag\(emulatorId, deleteCandidateTag\.id\)/);
  assert.doesNotMatch(source, /onClick=\{\(\) => void deleteTag\(emulatorId, tag\.id\)\}/);
});
