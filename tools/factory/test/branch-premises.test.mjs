import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseBranch } from '../lib/common.mjs';
import { checkFactoryVersion, checkGate, checkRole } from '../checks/branch.mjs';
import { checkPremises } from '../checks/premises.mjs';

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
  assert.deepEqual(checkGate('impl/46-x', ['stage:仕様確定', 'gate2-approved']), []);
  assert.match(checkGate('impl/46-x', ['gate1-approved'])[0].message, /gate2-approved が付いていない/);
  assert.match(checkGate('spec/46-x', [])[0].message, /gate1-approved/);
  assert.match(checkGate('req/46-x', [])[0].message, /gate0-approved/);
  assert.match(checkGate('req/46-x', null)[0].message, /見つからない/);
  for (const b of ['knowledge/46-x', 'prep/46-x', 'factory/x']) assert.deepEqual(checkGate(b, []), [], b);
});

test('factory-version: 工場の設定を変えたら CHANGELOG が要る', () => {
  assert.deepEqual(checkFactoryVersion('factory/x', ['.claude/agents/a.md', '.factory/CHANGELOG.md']), []);
  assert.deepEqual(checkFactoryVersion('impl/46-x', ['backend/src/A.cs']), []);
  assert.deepEqual(checkFactoryVersion('dependabot/github_actions/x', ['.github/workflows/ci.yml']), []);
  assert.match(checkFactoryVersion('factory/x', ['AGENTS.md', 'tools/factory/cli.mjs'])[0].message, /AGENTS.md、tools\/factory\/cli.mjs/);
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
