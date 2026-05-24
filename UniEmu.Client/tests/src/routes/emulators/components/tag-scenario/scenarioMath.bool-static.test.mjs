import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { Buffer } from 'node:buffer';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

async function importScenarioMath() {
  const source = await readFile(join(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..', '..', 'src', 'routes', 'emulators', 'components', 'tag-scenario'), 'scenarioMath.ts'), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.ES2022,
      target: ts.ScriptTarget.ES2022,
    },
  });

  return import(`data:text/javascript;base64,${Buffer.from(outputText).toString('base64')}`);
}

test('static bool scenario values are sampled as 0 or 1', async () => {
  const { sampleScenario, valueAt } = await importScenarioMath();
  const trueScenario = {
    startValue: 'false',
    continueOnFormulaEnd: 'Repeat',
    segments: [
      {
        id: 'seg-true',
        duration: 10,
        calc: { type: 'Static', start: 'true', duration: 10 },
      },
    ],
  };
  const falseScenario = {
    ...trueScenario,
    segments: [
      {
        id: 'seg-false',
        duration: 10,
        calc: { type: 'Static', start: 'false', duration: 10 },
      },
    ],
  };

  assert.deepEqual(
    sampleScenario(trueScenario, 3).map((point) => point.value),
    [1, 1, 1],
  );
  assert.equal(valueAt(trueScenario, 5), 1);
  assert.deepEqual(
    sampleScenario(falseScenario, 3).map((point) => point.value),
    [0, 0, 0],
  );
  assert.equal(valueAt(falseScenario, 5), 0);
});

test('scenario sampling tolerates null static values from persisted configs', async () => {
  const { sampleScenario, valueAt } = await importScenarioMath();
  const scenario = {
    startValue: null,
    continueOnFormulaEnd: 'Repeat',
    segments: [
      {
        id: 'seg-null',
        duration: 10,
        calc: { type: 'Static', start: null, duration: 10 },
      },
    ],
  };

  assert.deepEqual(
    sampleScenario(scenario, 3).map((point) => point.value),
    [0, 0, 0],
  );
  assert.equal(valueAt(scenario, 5), 0);
});

test('string scenario values preserve static text for timeline previews', async () => {
  const { scenarioValueAt, formatScenarioPreviewValue } = await importScenarioMath();
  const scenario = {
    startValue: '',
    continueOnFormulaEnd: 'Repeat',
    segments: [
      {
        id: 'seg-heating',
        duration: 300,
        label: 'Heating',
        calc: { type: 'Static', start: 'Heating', duration: 300 },
      },
      {
        id: 'seg-soaking',
        duration: 360,
        label: 'Soaking',
        calc: { type: 'Static', start: 'Soaking', duration: 360 },
      },
    ],
  };

  assert.equal(scenarioValueAt(scenario, 'string', 60), 'Heating');
  assert.equal(scenarioValueAt(scenario, 'string', 330), 'Soaking');
  assert.equal(formatScenarioPreviewValue('string', 'Heating'), 'Heating');
  assert.equal(formatScenarioPreviewValue('bool', 1), '1');
  assert.equal(formatScenarioPreviewValue('double', 1.234), '1.23');
});
