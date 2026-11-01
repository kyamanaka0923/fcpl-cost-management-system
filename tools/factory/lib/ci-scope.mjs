// PR で変えた場所から、動かす CI のジョブを決める(#73)。依存のない純粋な関数にして、CI の最初のジョブでそのまま動かす。
// 迷う場所(どの決まりにも当たらないファイル)は、すべてのジョブを動かす側に倒す。

/** 判定の対象のジョブ(.github/workflows/ci.yml と factory.yml の画面の差分)。 */
export const SCOPES = ['backend', 'frontend', 'frontendUnit', 'browser', 'screens', 'mutation', 'metrics'];

const BACKEND_DIRS = ['backend/', 'tests/unit/backend/', 'tests/integration/', 'tests/e2e/api/', 'tests/architecture/', 'tests/.config/'];
const REQNROLL_FEATURE = /^tests\/(integration|e2e\/api)\/.+\.feature$/;
const DOC_ONLY = ['knowledge/', 'requirements/', 'specs/', 'docs/', '.factory/', '.github/ISSUE_TEMPLATE/'];
const DOC_FILES = ['.github/pull_request_template.md', '.github/dependabot.yml', '.gitignore', 'LICENSE'];

/** 1 つのファイルが求めるジョブ。'all' はすべて。 */
export function scopeOfFile(file) {
  if (file === 'docs/api/openapi.json') return ['backend', 'frontend', 'screens'];
  // Reqnroll の .feature はテストとしてビルドされる。要件の PR でも動かす(@未実装 の付け忘れはここで見つかる。#59)
  if (REQNROLL_FEATURE.test(file)) return ['backend'];
  if (file === '.editorconfig' || BACKEND_DIRS.some((d) => file.startsWith(d))) return ['backend', 'browser', 'mutation', 'metrics'];
  if (file.startsWith('frontend/')) return ['frontend', 'frontendUnit', 'browser', 'screens', 'metrics'];
  if (file.startsWith('tests/unit/frontend/')) return ['frontendUnit', 'metrics'];
  if (file.startsWith('tests/e2e/browser/')) return ['browser'];
  if (file.startsWith('requirements/screens/') || file.startsWith('tests/e2e/screens/')) return ['screens'];
  if (file.endsWith('.md') || DOC_FILES.includes(file) || DOC_ONLY.some((d) => file.startsWith(d))) return [];
  return 'all';
}

/**
 * 変えたファイルの一覧から、ジョブごとに動かすかを決める。
 * @param {string[] | null} files PR で変えたファイル。null(PR でない・差分が取れない)ならすべて動かす
 * @returns {Record<string, boolean>}
 */
export function ciScope(files) {
  const on = new Set();
  for (const f of files ?? [null]) {
    const s = f === null ? 'all' : scopeOfFile(f);
    for (const name of s === 'all' ? SCOPES : s) on.add(name);
  }
  return Object.fromEntries(SCOPES.map((name) => [name, on.has(name)]));
}
