import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

test('tag table constrains content columns so row actions stay inside narrow screens', async () => {
  const source = await readFile(
    join(
      join(
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
      ),
      'EmulatorTagsTab.tsx'
    ),
    'utf8'
  );

  assert.match(source, /<div className="overflow-x-auto">/);
  assert.match(source, /min-w-\[62rem\] table-fixed/);
  assert.match(source, /<colgroup>[\s\S]*w-\[clamp\(7rem,11vw,8\.75rem\)\][\s\S]*w-\[8\.75rem\][\s\S]*<\/colgroup>/);
  assert.match(source, /<col className="w-\[6rem\]" \/>/);
  assert.match(source, /<col className="w-\[6\.5rem\]" \/>[\s\S]*<col className="w-\[6\.5rem\]" \/>/);
  assert.match(source, /<span className="inline-block rounded bg-muted px-2 py-0\.5 font-mono text-\[10px\] uppercase leading-tight">/);
  assert.match(
    source,
    /<td className="px-3 py-3 align-middle break-words text-xs text-muted-foreground \[overflow-wrap:anywhere\]">[\s\S]*\{getTagSourceLabel\(tag\.source\)\}/
  );
  assert.match(
    source,
    /<td className="px-3 py-3 align-middle break-words text-xs text-muted-foreground \[overflow-wrap:anywhere\]">[\s\S]*\{formatTrigger\(tag\.trigger, tag\.source\)\}/
  );
  assert.match(source, /<td className="px-3 py-3 align-middle break-words font-mono text-\[11px\] text-muted-foreground \[overflow-wrap:anywhere\]">/);
  assert.match(source, /<span title=\{tag\.name\} className="block max-w-full truncate">/);
  assert.match(source, /<span title=\{tag\.key === 'Custom' \? '-' : tag\.key\} className="block max-w-full truncate">/);
  assert.match(source, /className="h-7 w-full min-w-0 max-w-\[18rem\] px-2 py-1 font-mono text-xs"/);
  assert.match(source, /className="flex w-full items-center justify-end gap-2 whitespace-nowrap"/);
  assert.doesNotMatch(source, /<table className=\{`w-full text-sm/);
});
