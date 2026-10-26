import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseFeature } from '../lib/gherkin.mjs';
import { audit, matrixMarkdown } from '../checks/coverage.mjs';

const SENTENCE = 'もし明細の金額が 0 未満ならば、システムは、その明細の保存を拒否する。';
const reqs = (status = '有効') =>
  new Map([['FR-4.8', { file: 'requirements/budget/FR-4.8.md', data: { title: '金額は 0 以上', status }, sentence: SENTENCE }]]);
const brs = new Set(['BR-BUD-007']);
const feature = (body) => [{ file: 'tests/x.feature', parsed: parseFeature(`# language: ja\n機能: 予算\n\n${body}`) }];

test('ルールに付けたタグは配下のシナリオすべてに効く', () => {
  const p = parseFeature(`# language: ja\n@機能タグ\n機能: 予算\n\n  @FR-4.8 @BR-BUD-007\n  ルール: ${SENTENCE}\n\n    シナリオ: A\n      もし x\n\n    @個別\n    シナリオ: B\n      もし y\n`);
  assert.equal(p.scenarios.length, 2);
  assert.deepEqual(p.scenarios[0].tags, ['機能タグ', 'FR-4.8', 'BR-BUD-007']);
  assert.deepEqual(p.scenarios[1].tags, ['機能タグ', 'FR-4.8', 'BR-BUD-007', '個別']);
});

test('ステップを挟んだタグは次のキーワードに付かない', () => {
  const p = parseFeature('機能: x\n  シナリオ: A\n    もし y\n    @FR-1.1\n    | 列 |\n  シナリオ: B\n');
  assert.deepEqual(p.scenarios[1].tags, []);
});

test('要件文どおりのルールの下にシナリオがあれば通り、マトリクスに出る', () => {
  const { errors, matrix } = audit(reqs(), brs, feature(`  @FR-4.8 @BR-BUD-007\n  ルール: ${SENTENCE}\n\n    シナリオ: -1 円は保存できない\n      もし x\n`));
  assert.deepEqual(errors, []);
  assert.equal(matrix[0].scenarios.length, 1);
  assert.match(matrixMarkdown(matrix), /\| FR-4.8 \| 金額は 0 以上 \| 有効 \| 1 \| tests\/x.feature:7 \|/);
});

test('シナリオのない要件を指摘する', () => {
  const { errors } = audit(reqs(), brs, feature('  シナリオ: 関係ない\n'));
  assert.match(errors[0].message, /FR-4.8 を確かめるシナリオがない/);
});

test('廃止した要件はシナリオがなくても通るが、タグで指すと指摘する', () => {
  assert.deepEqual(audit(reqs('廃止'), brs, feature('  シナリオ: 関係ない\n')).errors, []);
  const { errors } = audit(reqs('廃止'), brs, feature('  @FR-4.8\n  シナリオ: A\n'));
  assert.match(errors[0].message, /廃止した要件/);
});

test('孤児のタグ・ない業務ルールのタグを指摘する', () => {
  const { errors } = audit(reqs(), brs, feature(`  @FR-4.8 @BR-BUD-007\n  ルール: ${SENTENCE}\n    シナリオ: A\n\n  @FR-9.9 @BR-XXX-001\n  シナリオ: B\n`));
  const msgs = errors.map((e) => e.message).join('\n');
  assert.match(msgs, /@FR-9.9 は requirements\/ にない要件/);
  assert.match(msgs, /@BR-XXX-001 は knowledge\/business-rules\/ にない/);
});

test('ルールの文が要件文と違う・1 ルールに 2 要件・シナリオのないルールを指摘する', () => {
  const two = new Map([...reqs(), ['FR-4.9', { file: 'f', data: { title: 't', status: '有効' }, sentence: 's' }]]);
  const { errors } = audit(two, brs, feature(`  @FR-4.8\n  ルール: 金額は 0 以上\n    シナリオ: A\n\n  @FR-4.8 @FR-4.9\n  ルール: 2 つ\n    シナリオ: B\n\n  @FR-4.9\n  ルール: s\n`));
  const msgs = errors.map((e) => e.message).join('\n');
  assert.match(msgs, /要件文と違う/);
  assert.match(msgs, /1 つの「ルール:」には 1 つの要件/);
  assert.match(msgs, /FR-4.9 の「ルール:」の下にシナリオがない/);
});
