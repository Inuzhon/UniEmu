import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('sidebar uses TanStack links for navigation items', async () => {
  const source = await readFile(
    join(dirname(fileURLToPath(import.meta.url)), 'Sidebar.tsx'),
    'utf8'
  );

  assert.match(source, /import \{[^}]*\bLink\b[^}]*\} from ['"]@tanstack\/react-router['"]/);
  assert.doesNotMatch(source, /import \{[^}]*\bLink\b[^}]*\} from ['"]lucide-react['"]/);
});
