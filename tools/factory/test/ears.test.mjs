import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkRequirementFile, lintSentence } from '../checks/ears.mjs';

test('EARS の 5 文型と複合の前置きは通る', () => {
  for (const s of [
    'システムは、課の区分合計を明細の合計として算出する。',
    '改定版が承認されたとき、システムは、同じ課と半期の旧承認版を失効にする。',
    '予算が承認済みである間、システムは、その予算の明細の変更を拒否する。',
    'もし明細の金額が 0 未満ならば、システムは、その明細の保存を拒否する。',
    '期間費用の明細が明細名を含む場合、システムは、費目合計を明細の合計として算出する。',
    '予算がドラフトである間、明細が保存されたとき、システムは、区分合計を算出し直す。',
  ]) {
    assert.deepEqual(lintSentence(s), [], s);
  }
});

test('文型に合わない要件文は、理由を付けて拒否される', () => {
  const cases = [
    ['システムは、明細を保存する', '「。」がない'],
    ['システムは、明細を保存する。区分合計を出す。', '1 文にする'],
    ['明細を保存できる。', '主語「システムは、」がない'],
    ['明細が保存されたら、システムは、区分合計を算出する。', '前置きが EARS の文型に合わない'],
    ['金額が負ならば、システムは、保存を拒否する。', '「もし」で始める'],
    ['システムは、明細を保存し、区分合計を算出する。', '読点'],
    ['システムは、明細を保存しかつ区分合計を算出する。', '応答が 2 つある'],
  ];
  for (const [s, reason] of cases) {
    const errors = lintSentence(s);
    assert.ok(errors.some((e) => e.includes(reason)), `${s} → ${errors.join(' / ')}`);
  }
});

const file = (fm, body = '## 要件\n\nシステムは、課の区分合計を明細の合計として算出する。\n') => `---\n${fm}\n---\n\n${body}`;
const goodFm = 'id: FR-4.5\ntitle: 区分合計は明細の合計\npriority: Must\nstatus: 有効\nbusiness_rules: [BR-BUD-004]\nscreens: []\nissues: []';

test('形のそろった要件のファイルは通る', () => {
  assert.deepEqual(checkRequirementFile('requirements/budget/FR-4.5.md', file(goodFm)), []);
});

test('枝番の ID は通る', () => {
  const fm = goodFm.replace('FR-4.5', 'FR-4.4a-1');
  assert.deepEqual(checkRequirementFile('requirements/budget/FR-4.4a-1.md', file(fm)), []);
});

test('ID とファイル名・領域のずれ、欠けた項目、行数の誤りを指摘する', () => {
  const msgs = (f, t) => checkRequirementFile(f, t).map((e) => e.message).join('\n');
  assert.match(msgs('requirements/budget/FR-4.6.md', file(goodFm)), /ファイル名/);
  assert.match(msgs('requirements/actual/FR-4.5.md', file(goodFm)), /requirements\/budget\/ に置く/);
  assert.match(msgs('requirements/budget/FR-4.5.md', file(goodFm.replace('priority: Must', 'priority: 高'))), /priority/);
  assert.match(msgs('requirements/budget/FR-4.5.md', file(goodFm.replace('screens: []', 'screens: SCR-001'))), /screens は配列/);
  assert.match(msgs('requirements/budget/FR-4.5.md', file(goodFm, '## 要件\n\nシステムは、A を出す。\nシステムは、B を出す。\n')), /1 行だけ/);
  assert.match(msgs('requirements/budget/FR-4.5.md', file(goodFm, '## 補足\n\nなし\n')), /要件文がない/);
  assert.match(msgs('requirements/budget/FR-4.5.md', 'frontmatter なし'), /frontmatter/);
});
