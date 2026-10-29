import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ciScope, SCOPES } from '../lib/ci-scope.mjs';

const on = (files) => Object.entries(ciScope(files)).filter(([, v]) => v).map(([k]) => k).sort();
const all = [...SCOPES].sort();

test('ナレッジ・要件の Markdown・仕様・文書だけの PR では、プロダクトのジョブを動かさない', () => {
  assert.deepEqual(on(['knowledge/business-rules/BR-BUD-007.md', 'requirements/budget/FR-4.13.md', 'specs/test-plan.md', 'README.md', 'docs/factory/README.md', '.factory/changes/73.md']), []);
});

test('要件の PR で Reqnroll の .feature を変えたら、バックエンドのテストを動かす', () => {
  assert.deepEqual(on(['requirements/budget/FR-4.13.md', 'tests/integration/backend/Application/Features/課予算の策定と改定.feature']), ['backend']);
  assert.deepEqual(on(['tests/e2e/api/Features/APIのエラー応答.feature']), ['backend']);
});

test('ブラウザE2E の .feature はブラウザE2E だけ、画面要件は画面の差分だけ', () => {
  assert.deepEqual(on(['tests/e2e/browser/features/予算明細の入力.feature']), ['browser']);
  assert.deepEqual(on(['requirements/screens/screens.yaml', 'requirements/screens/fixtures/SCR-001/通常.json']), ['screens']);
});

test('バックエンドのコードはバックエンド・ブラウザE2E・ミューテーション・メトリクス', () => {
  assert.deepEqual(on(['backend/src/CostManagement.Domain/Budgeting/DepartmentBudget.cs']), ['backend', 'browser', 'metrics', 'mutation']);
  assert.deepEqual(on(['tests/unit/backend/Domain/X.cs']), ['backend', 'browser', 'metrics', 'mutation']);
});

test('フロントエンドのコードはフロント・Vitest・ブラウザE2E・画面の差分・メトリクス', () => {
  assert.deepEqual(on(['frontend/src/pages/BudgetEditPage.tsx']), ['browser', 'frontend', 'frontendUnit', 'metrics', 'screens']);
  assert.deepEqual(on(['tests/unit/frontend/src/a.test.ts']), ['frontendUnit', 'metrics']);
});

test('OpenAPI 定義はバックエンド(一致の検査)・フロント(型)・画面の差分(fixtures)', () => {
  assert.deepEqual(on(['docs/api/openapi.json']), ['backend', 'frontend', 'screens']);
});

test('CI・工場の道具・Claude の設定・どれにも当たらないファイルは、すべてのジョブを動かす', () => {
  for (const f of ['.github/workflows/ci.yml', 'tools/factory/cli.mjs', '.claude/hooks/guard.sh', 'docker/dev.Dockerfile', 'infra/template.yaml', 'global.json']) {
    assert.deepEqual(on([f]), all, f);
  }
});

test('PR でない(差分がない)ときは、すべてのジョブを動かす', () => {
  assert.deepEqual(on(null), all);
});

test('空の差分(変更なし)では何も動かさない', () => {
  assert.deepEqual(on([]), []);
});
