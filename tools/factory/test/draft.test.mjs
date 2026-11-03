import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findRun, parseStatusZ, splitByRole } from '../lib/draft.mjs';

test('git status -z の出力から、空白や日本語を含むパスを取り出す', () => {
  const out = ' M backend/src/A.cs\0?? tests/e2e/browser/steps/楽観ロック.steps.ts\0 D docs/a b.md\0';
  assert.deepEqual(parseStatusZ(out), ['backend/src/A.cs', 'tests/e2e/browser/steps/楽観ロック.steps.ts', 'docs/a b.md']);
  assert.deepEqual(parseStatusZ(''), []);
});

test('書きかけを、工程の持ち場の中と外に分ける', () => {
  const files = ['backend/src/A.cs', 'tests/unit/x.cs', '.factory/premises/ISSUE-45.yml', 'specs/rules/SPEC-004.md', '.claude/x.md'];
  assert.deepEqual(splitByRole('impl/45-takt', files), {
    inside: ['backend/src/A.cs', 'tests/unit/x.cs', '.factory/premises/ISSUE-45.yml'],
    outside: ['specs/rules/SPEC-004.md', '.claude/x.md'],
  });
  assert.deepEqual(splitByRole('spec/45-takt', ['specs/api/ISSUE-45.openapi.json', 'docs/api/openapi.json']), {
    inside: ['specs/api/ISSUE-45.openapi.json'],
    outside: ['docs/api/openapi.json'],
  });
});

test('命名に合わないブランチでは、何もコミットしない(すべて外にする)', () => {
  assert.deepEqual(splitByRole('main', ['backend/src/A.cs']), { inside: [], outside: ['backend/src/A.cs'] });
});

test('この実行の run を、タスクの本文と開始時刻で選ぶ(同じタスクの前の run は拾わない)', () => {
  const task = '#45\n- 工程: [D] 実装(impl)';
  const metas = [
    { dir: 'old', task, status: 'failed', startTime: '2026-11-03T02:38:12.325Z' },
    { dir: 'other', task: '#46', status: 'running', startTime: '2026-11-03T06:30:00.000Z' },
    { dir: 'new', task, status: 'failed', startTime: '2026-11-03T06:21:31.000Z' },
  ];
  assert.equal(findRun(metas, task, Date.parse('2026-11-03T06:21:00Z')).dir, 'new');
  assert.equal(findRun(metas, task, Date.parse('2026-11-03T07:00:00Z')), null);
});
