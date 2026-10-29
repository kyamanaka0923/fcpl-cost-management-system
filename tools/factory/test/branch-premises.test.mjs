import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseBranch } from '../lib/common.mjs';
import { checkFactoryVersion, checkGate, checkRole } from '../checks/branch.mjs';
import { checkPremises } from '../checks/premises.mjs';
import { changesMarkdown } from '../lib/changes.mjs';

test('ブランチ名から工程と Issue 番号を読む', () => {
  assert.deepEqual(parseBranch('impl/46-fraction'), { role: 'impl', issue: 46 });
  assert.deepEqual(parseBranch('factory/phase4'), { role: 'factory', issue: null });
  assert.deepEqual(parseBranch('dependabot/npm_and_yarn/x'), { role: 'dependabot', issue: null });
  assert.equal(parseBranch('claude/cost-management-system-qvc368'), null);
  assert.equal(parseBranch('impl/fraction'), null);
});

test('role-guard: 工程の持ち場の中の変更は通る', () => {
  assert.deepEqual(checkRole('knowledge/45-lock', ['knowledge/business-rules/BR-BUD-008.md']), []);
  assert.deepEqual(checkRole('req/46-x', ['requirements/budget/FR-4.8.md', 'tests/e2e/api/Features/APIのエラー応答.feature', 'docs/REQUIREMENTS.md']), []);
  assert.deepEqual(checkRole('spec/46-x', ['specs/rules/SPEC-001.md', 'docs/api/openapi.json', '.factory/premises/ISSUE-46.yml']), []);
  assert.deepEqual(checkRole('impl/46-x', ['backend/src/A.cs', 'frontend/src/a.ts', 'tests/unit/x.cs', 'docs/MANUAL.md', '.factory/premises/ISSUE-46.yml']), []);
  assert.deepEqual(checkRole('factory/x', ['.claude/agents/a.md', 'tools/factory/cli.mjs', 'requirements/README.md', 'knowledge/README.md']), []);
  assert.deepEqual(checkRole('dependabot/nuget/x', ['backend/Directory.Packages.props']), []);
});

test('role-guard: 持ち場の外の変更を指摘する', () => {
  const files = (branch, list) => checkRole(branch, list).map((e) => e.file);
  assert.deepEqual(files('impl/46-x', ['backend/src/A.cs', 'requirements/budget/FR-4.8.md', 'specs/rules/SPEC-001.md', 'knowledge/glossary.md', 'docs/REQUIREMENTS.md', '.claude/x.md']), [
    'requirements/budget/FR-4.8.md',
    'specs/rules/SPEC-001.md',
    'knowledge/glossary.md',
    'docs/REQUIREMENTS.md',
    '.claude/x.md',
  ]);
  assert.deepEqual(files('req/46-x', ['backend/src/A.cs', 'tests/unit/x.cs']), ['backend/src/A.cs', 'tests/unit/x.cs']);
  assert.deepEqual(files('spec/46-x', ['tests/e2e/a.feature', 'requirements/budget/FR-4.8.md']), ['tests/e2e/a.feature', 'requirements/budget/FR-4.8.md']);
  assert.deepEqual(files('factory/x', ['backend/src/A.cs', 'knowledge/business-rules/BR-BUD-001.md', 'requirements/budget/FR-4.8.md', 'specs/rules/SPEC-001.md']), [
    'backend/src/A.cs',
    'knowledge/business-rules/BR-BUD-001.md',
    'requirements/budget/FR-4.8.md',
    'specs/rules/SPEC-001.md',
  ]);
});

test('role-guard: 命名に合わないブランチは落とす', () => {
  assert.match(checkRole('claude/cost-management-system-qvc368', ['README.md'])[0].message, /工場の命名に合わない/);
});

test('gate-check: 前のゲートのラベルがあれば通り、なければ指摘する', () => {
  const all = ['stage:仕様確定', 'gate0-approved', 'gate1-approved', 'gate2-approved'];
  const merged = ['knowledge/46-a', 'req/46-b', 'spec/46-c'];
  assert.deepEqual(checkGate('impl/46-x', all, merged), []);
  assert.match(checkGate('impl/46-x', ['gate1-approved'], merged)[0].message, /gate0-approved・gate2-approved が付いていない/);
  assert.match(checkGate('spec/46-x', [], merged)[0].message, /gate0-approved・gate1-approved/);
  assert.match(checkGate('req/46-x', [], merged)[0].message, /gate0-approved/);
  assert.match(checkGate('req/46-x', null)[0].message, /見つからない/);
  for (const b of ['knowledge/46-x', 'prep/46-x', 'factory/x']) assert.deepEqual(checkGate(b, []), [], b);
});

const change = (issue, kind, body = 'ブラウザE2E の Chromium をキャッシュする') => `---\nissue: ${issue}\nkind: ${kind}\n---\n${body}\n`;

test('factory-version: 工場の設定を変えたら、変更の記録(.factory/changes/)が要る', () => {
  const read = (f) => ({ '.factory/changes/66.md': change(66, 'C'), '.factory/changes/46-scenarios.md': change(46, 'A') })[f] ?? null;
  assert.deepEqual(checkFactoryVersion('factory/x', ['.claude/agents/a.md', '.factory/changes/66.md'], read), []);
  assert.deepEqual(checkFactoryVersion('factory/x', ['tools/factory/cli.mjs', '.factory/changes/46-scenarios.md'], read), []);
  assert.deepEqual(checkFactoryVersion('impl/46-x', ['backend/src/A.cs']), []);
  assert.deepEqual(checkFactoryVersion('dependabot/github_actions/x', ['.github/workflows/ci.yml']), []);
  assert.match(checkFactoryVersion('factory/x', ['AGENTS.md', 'tools/factory/cli.mjs'])[0].message, /AGENTS.md、tools\/factory\/cli.mjs/);
});

test('factory-version: CHANGELOG に行を足すだけでは通らない(衝突のもとなので、記録はファイルに分ける)', () => {
  const errors = checkFactoryVersion('factory/x', ['.claude/agents/a.md', '.factory/CHANGELOG.md']);
  assert.equal(errors.length, 1);
  assert.match(errors[0].message, /\.factory\/changes\/<Issue番号>\.md を足す/);
});

test('factory-version: 変更の記録の形の誤りを指摘する', () => {
  const files = {
    '.factory/changes/66.md': change(65, 'E', ''),
    '.factory/changes/67.md': 'frontmatter なし',
  };
  const read = (f) => files[f] ?? null;
  const msgs = checkFactoryVersion('factory/x', ['AGENTS.md', '.factory/changes/66.md', '.factory/changes/67.md', '.factory/changes/68.md'], read).map((e) => e.message);
  assert.equal(msgs.length, 5, msgs.join('\n'));
  assert.ok(msgs.some((m) => /issue が 65。ファイル名の番号 66/.test(m)));
  assert.ok(msgs.some((m) => /kind が E/.test(m)));
  assert.ok(msgs.some((m) => /本文に/.test(m)));
  assert.ok(msgs.some((m) => /frontmatter\(issue と kind\)がない/.test(m)));
  assert.ok(msgs.some((m) => /消さない/.test(m)));
});

test('変更の記録の一覧は Issue の番号の順の表になる', () => {
  const md = changesMarkdown([
    { file: '.factory/changes/66.md', text: change(66, 'C', '記録を\n1 件 1 ファイルにする') },
    { file: '.factory/changes/63.md', text: change(63, 'A', 'a | b') },
    { file: '.factory/changes/9.md', text: '壊れた記録' },
  ]);
  assert.equal(md, '| Issue | 種類 | 中身 |\n|---|---|---|\n| #63 | A | a \\| b |\n| #66 | C | 記録を 1 件 1 ファイルにする |');
  assert.equal(changesMarkdown([]), '変更の記録はまだない');
});

const premises = (issue, stage, list) =>
  `issue: ${issue}\nstage: ${stage}\nfactory_version: factory-v1\npremises:\n${list.map(([p, b]) => `  - path: ${p}\n    blob: ${b}`).join('\n')}\n`;
const main = { 'requirements/budget/FR-4.8.md': 'aaaaaaa1', 'specs/rules/SPEC-001.md': 'bbbbbbb2' };
const blobOnMain = (p) => main[p] ?? null;

test('upstream-approved: 前提の版が main と一致すれば通る', () => {
  const text = premises(46, 'impl', [['requirements/budget/FR-4.8.md', 'aaaaaaa1'], ['specs/rules/SPEC-001.md', 'bbbbbbb2']]);
  assert.deepEqual(checkPremises('impl/46-x', text, blobOnMain), []);
});

test('upstream-approved: 前提がない・main で変わった・main にない・工程や番号のずれを指摘する', () => {
  assert.match(checkPremises('impl/46-x', null, blobOnMain)[0].message, /前提の記録がない/);
  const msgs = checkPremises('impl/46-x', premises(47, 'spec', [['requirements/budget/FR-4.8.md', 'old0000'], ['specs/rules/SPEC-002.md', 'ccc']]), blobOnMain)
    .map((e) => e.message)
    .join('\n');
  assert.match(msgs, /issue\(47\)/);
  assert.match(msgs, /stage\(spec\)/);
  assert.match(msgs, /FR-4.8.md が前提を書いたあとで main で変わった/);
  assert.match(msgs, /SPEC-002.md が main にない/);
});

test('upstream-approved: spec・impl 以外のブランチは見ない', () => {
  for (const b of ['req/46-x', 'knowledge/46-x', 'factory/x']) assert.deepEqual(checkPremises(b, null, blobOnMain), [], b);
});

test('gate-check: ゲートを飛ばしたラベル(gate2 だけ)では実装に着手できない(#77)', () => {
  const msgs = checkGate('impl/46-x', ['stage:仕様確定', 'gate2-approved'], ['req/46-b', 'spec/46-c']).map((e) => e.message);
  assert.equal(msgs.length, 1);
  assert.match(msgs[0], /gate0-approved・gate1-approved が付いていない/);
});

test('gate-check: ゲートラベルがあっても、その工程の PR がマージされていなければ落とす(#77)', () => {
  const labels = ['stage:仕様確定', 'gate0-approved', 'gate1-approved', 'gate2-approved'];
  const msgs = checkGate('impl/46-x', labels, ['req/46-b', 'spec/47-other']).map((e) => e.message);
  assert.equal(msgs.length, 1);
  assert.match(msgs[0], /gate2-approved が付いているが、spec\/46-… の PR が main にマージされていない/);
  assert.match(checkGate('impl/46-x', labels, null).at(-1).message, /取れなかった/);
});
