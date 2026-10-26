import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseIntervention } from '../lib/intervention.mjs';
import { computeReport, countOpenQuestions, flowOf, jst, median, reportMarkdown } from '../lib/metrics.mjs';

test('介入の記録を読む', () => {
  assert.deepEqual(parseIntervention('/intervention 差し戻し 仕様 端数の扱いがない\n詳しくは…'), { ok: true, kind: '差し戻し', cause: '仕様', note: '端数の扱いがない' });
  assert.deepEqual(parseIntervention('/intervention CI承認 工場'), { ok: true, kind: 'CI承認', cause: '工場', note: '' });
  assert.equal(parseIntervention('ふつうのコメント /intervention 回答 要件'), null);
  assert.equal(parseIntervention('/interventions 回答 要件'), null);
  assert.match(parseIntervention('/intervention 相談 要件').error, /種類「相談」/);
  assert.match(parseIntervention('/intervention 回答').error, /原因「」/);
});

test('未回答の問いは「回答」の列が空の行だけを数える', () => {
  const text = [
    '| ID | 問い | 選択肢 | 仮決め | 範囲 | 関係 | 回答 | 回答日 |',
    '|---|---|---|---|---|---|---|---|',
    '| OQ-001 | a | A/B | なし | x | y | A | 2026-10-26 |',
    '| OQ-002 | b | A/B | なし | x | y |  |  |',
    '| OQ-003 | c | A/B | なし | x | y | | |',
  ].join('\n');
  assert.equal(countOpenQuestions(text), 2);
});

test('中央値と JST の表示', () => {
  assert.equal(median([3, 1, 2]), 2);
  assert.equal(median([4, 1, null, 2, 3]), 2.5);
  assert.equal(median([null]), null);
  assert.equal(jst('2026-10-26T12:54:12Z'), '2026-10-26 21:54');
});

const ev = (label, at, action = 'labeled') => ({ label, action, at });
const done = {
  number: 46,
  title: '端数',
  state: 'closed',
  createdAt: '2026-10-01T00:00:00Z',
  closedAt: '2026-10-11T00:00:00Z',
  labels: ['stage:判断待ち'],
  labelEvents: [
    ev('stage:準備中', '2026-10-01T00:00:00Z'),
    ev('gate0-approved', '2026-10-02T00:00:00Z'),
    ev('gate1-approved', '2026-10-04T00:00:00Z'),
    ev('needs-answer', '2026-10-05T00:00:00Z'),
    ev('gate2-approved', '2026-10-07T12:00:00Z'),
  ],
};

test('作業項目のゲートごとの日数', () => {
  assert.deepEqual(flowOf(done), { number: 46, title: '端数', state: 'closed', toGate0: 1, toGate1: 2, toGate2: 3.5, toDone: 3.5, total: 10 });
  const skipped = flowOf({ ...done, labelEvents: [ev('gate2-approved', '2026-10-03T00:00:00Z')] });
  assert.equal(skipped.toGate0, null);
  assert.equal(skipped.toDone, 8);
});

const base = (over = {}) => ({
  now: new Date('2026-10-12T00:00:00Z'),
  windowDays: 7,
  repo: 'o/r',
  issues: [
    done,
    { number: 47, title: '判断待ち', state: 'open', createdAt: '2026-10-06T00:00:00Z', closedAt: null, labels: ['stage:判断待ち', 'needs-answer'], labelEvents: [] },
    { number: 48, title: 'ラインの外', state: 'open', createdAt: '2026-10-06T00:00:00Z', closedAt: null, labels: ['bug'], labelEvents: [] },
  ],
  pulls: [
    { number: 1, headRef: 'impl/46-x', mergedAt: '2026-10-11T00:00:00Z' },
    { number: 2, headRef: 'spec/46-x', mergedAt: '2026-09-01T00:00:00Z' },
    { number: 3, headRef: 'dependabot/npm/x', mergedAt: '2026-10-10T00:00:00Z' },
  ],
  comments: [
    { issueNumber: 46, body: '/intervention 手直し 仕様 端数', createdAt: '2026-10-09T00:00:00Z', url: 'u1' },
    { issueNumber: 46, body: '/intervention 回答 要件', createdAt: '2026-10-01T00:00:00Z', url: 'u2' },
    { issueNumber: 47, body: '/intervention 相談 要件', createdAt: '2026-10-10T00:00:00Z', url: 'u3' },
  ],
  openQuestionsText: '| OQ-009 | q | A/B | x | y | z |  |  |',
  ...over,
});

test('週報: WIP・流れ・期間中のできごと・介入を数える', () => {
  const r = computeReport(base());
  assert.deepEqual(r.wip, { judging: 1, ready: 0, needsAnswer: 1, openQuestions: 1 });
  assert.equal(r.warnings.length, 1);
  assert.match(r.warnings[0], /着手待ちが 0 件/);
  assert.equal(r.medians.total, 10);
  assert.deepEqual(r.shown.map((f) => f.number), [46, 47]);
  assert.equal(r.merged.impl, 1);
  assert.equal(r.merged.spec, 0);
  assert.equal(r.merged['その他'], 1);
  assert.equal(r.counts.needsAnswer, 1); // 期間の始まりちょうど(10-05 00:00)に付けたものも数える
  assert.equal(r.counts.gates['gate2-approved'], 1);
  assert.deepEqual(r.interventions.map((i) => `${i.kind}・${i.cause}`), ['手直し・仕様']);
  assert.equal(r.invalid.length, 1);
});

test('週報: 判断待ちと未回答の問いが上限を超えたら知らせる', () => {
  const many = Array.from({ length: 6 }, (_, k) => ({ number: 100 + k, title: 't', state: 'open', createdAt: '2026-10-06T00:00:00Z', closedAt: null, labels: ['stage:判断待ち'], labelEvents: [] }));
  const questions = Array.from({ length: 11 }, (_, k) => `| OQ-${k} | q | a | b | c | d |  |  |`).join('\n');
  const r = computeReport(base({ issues: many, openQuestionsText: questions }));
  const text = r.warnings.join('\n');
  assert.match(text, /判断待ちが 6 件/);
  assert.match(text, /未回答の問いが 11 件/);
});

test('週報の Markdown に、表と手直しの注意と形の違う記録が出る', () => {
  const md = reportMarkdown(computeReport(base()));
  assert.match(md, /## 工場の週報 2026-10-05 09:00 〜 2026-10-12 09:00\(JST\)/);
  assert.match(md, /\| 1\.0 日 \| 2\.0 日 \| 3\.5 日 \| 3\.5 日 \| 10\.0 日 \|/);
  assert.match(md, /\| 手直し \| 0 \| 0 \| 1 \| 0 \|/);
  assert.match(md, /手で直したのは 1 件/);
  assert.match(md, /\[#47\]\(u3\) 種類「相談」/);
  assert.match(md, /マージした PR: impl 1、その他 1/);
});
