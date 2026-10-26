import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseSourceIds } from '../lib/common.mjs';
import { checkBusinessRule, checkRequirementRules } from '../checks/knowledge.mjs';
import { findAmbiguousWords, isAmbiguityTarget } from '../checks/ambiguity.mjs';

test('出どころの表の範囲 ID は桁をそろえて展開される', () => {
  const ids = parseSourceIds('| ID | 出どころ |\n|---|---|\n| SRC-REQ | 要件 |\n| SRC-ADR-0002〜0016 | ADR |\n| SRC-ANS-OQ-001〜003 | 回答 |\n');
  for (const id of ['SRC-REQ', 'SRC-ADR-0002', 'SRC-ADR-0009', 'SRC-ADR-0016', 'SRC-ANS-OQ-003']) assert.ok(ids.has(id), id);
  assert.ok(!ids.has('SRC-ADR-0017'));
  assert.ok(!ids.has('ID'));
});

const rule = (fm) => `---\n${fm}\n---\n\n## ルール\n`;
const sources = new Set(['SRC-REQ', 'SRC-CODE']);

test('出どころがあり形のそろった業務ルールは通る', () => {
  assert.deepEqual(checkBusinessRule('knowledge/business-rules/BR-BUD-007.md', rule('id: BR-BUD-007\ntitle: 金額\nstatus: 確定\nsources: [SRC-REQ]'), sources), []);
});

test('出どころがない・索引にない出どころ・status の誤り・ID のずれを指摘する', () => {
  const msgs = (fm, f = 'knowledge/business-rules/BR-BUD-007.md') => checkBusinessRule(f, rule(fm), sources).map((e) => e.message).join('\n');
  assert.match(msgs('id: BR-BUD-007\ntitle: t\nstatus: 確定\nsources: []'), /sources\(出どころ\)がない/);
  assert.match(msgs('id: BR-BUD-007\ntitle: t\nstatus: 確定\nsources: [SRC-NOPE]'), /SRC-NOPE が knowledge\/sources.md にない/);
  assert.match(msgs('id: BR-BUD-007\ntitle: t\nstatus: 仮\nsources: [SRC-REQ]'), /status/);
  assert.match(msgs('id: BR-BUD-007\ntitle: t\nstatus: 確定\nsources: [SRC-REQ]', 'knowledge/business-rules/BR-BUD-008.md'), /ファイル名/);
});

test('要件が未確定やない業務ルールを参照していたら指摘する', () => {
  const brs = new Map([
    ['BR-BUD-007', { data: { status: '確定' } }],
    ['BR-BUD-008', { data: { status: '未確定' } }],
  ]);
  const reqs = new Map([['FR-4.8', { file: 'r', data: { business_rules: ['BR-BUD-007', 'BR-BUD-008', 'BR-BUD-999'] } }]]);
  const msgs = checkRequirementRules(reqs, brs).map((e) => e.message);
  assert.equal(msgs.length, 2);
  assert.match(msgs[0], /未確定の業務ルール BR-BUD-008/);
  assert.match(msgs[1], /BR-BUD-999 がない/);
});

test('曖昧語は Markdown の全行と .feature の「ルール:」行だけで見つける', () => {
  const words = ['適切', 'など'];
  assert.equal(findAmbiguousWords('requirements/budget/FR-1.1.md', 'title: 適切に保存\n本文など\n', words).length, 2);
  const feature = '機能: 適切に動く\n  ルール: 金額などを保存する\n    シナリオ: 適切に\n';
  const found = findAmbiguousWords('tests/x.feature', feature, words);
  assert.deepEqual(found.map((e) => e.line), [2]);
});

test('README は曖昧語の検査から外す', () => {
  assert.ok(!isAmbiguityTarget('requirements/README.md'));
  assert.ok(isAmbiguityTarget('requirements/budget/FR-4.8.md'));
  assert.ok(isAmbiguityTarget('knowledge/business-rules/BR-BUD-007.md'));
  assert.ok(!isAmbiguityTarget('knowledge/glossary.md'));
  assert.ok(isAmbiguityTarget('tests/e2e/browser/features/計画策定.feature'));
});
