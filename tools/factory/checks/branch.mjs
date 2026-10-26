// PR のブランチに対する検査: role-guard(持ち場)・gate-check(ゲートラベル)・factory-version(工場の版)。
import { issue, parseBranch } from '../lib/common.mjs';

const FR_FILE = /^requirements\/(?!screens\/)[^/]+\/FR-[^/]+\.md$/;
const FEATURE = /^tests\/.+\.feature$/;

/**
 * 工程ごとに触ってよい場所(AGENTS.md「工程ごとの置き場とブランチ」)。
 * allow のどれかに当たり、deny のどれにも当たらないファイルだけを変えてよい。文字列は前方一致、RegExp は全体。
 */
export const ROLES = {
  knowledge: { allow: ['knowledge/'], deny: [] },
  req: { allow: ['requirements/', 'docs/REQUIREMENTS.md', 'knowledge/open-questions.md', FEATURE], deny: [] },
  spec: { allow: ['specs/', 'docs/api/openapi.json', '.factory/premises/'], deny: [] },
  impl: {
    allow: ['backend/', 'frontend/', 'tests/', 'docs/', 'infra/', 'docker/', 'docker-compose.yml', 'README.md', '.factory/premises/'],
    deny: ['docs/REQUIREMENTS.md', 'docs/adr/'],
  },
  prep: { allow: ['tests/', 'knowledge/open-questions.md'], deny: [] },
  factory: {
    allow: [/.*/],
    deny: ['backend/', 'frontend/src/', 'knowledge/business-rules/', 'knowledge/glossary.md', FR_FILE, /^specs\/rules\/SPEC-.+\.md$/],
  },
};

const hit = (file, pattern) => (typeof pattern === 'string' ? file.startsWith(pattern) : pattern.test(file));

/** role-guard: ブランチの工程が触ってよい場所だけを変えたか。 */
export function checkRole(branch, changedFiles) {
  const b = parseBranch(branch);
  if (!b) {
    return [issue('', 0, `ブランチ名「${branch}」が工場の命名に合わない(knowledge|req|spec|impl|prep/<Issue番号>-<名前>、factory/<名前>)`)];
  }
  if (b.role === 'dependabot') return [];
  const role = ROLES[b.role];
  return changedFiles
    .filter((f) => !role.allow.some((p) => hit(f, p)) || role.deny.some((p) => hit(f, p)))
    .map((f) => issue(f, 0, `${b.role}/ のブランチでは変えてはいけない場所(AGENTS.md「工程ごとの置き場とブランチ」)`));
}

/** ブランチの工程が着手するのに要るゲートラベル。 */
export const REQUIRED_GATE = { req: 'gate0-approved', spec: 'gate1-approved', impl: 'gate2-approved' };

/** gate-check: 着手するのに要るゲートを、Issue が通っているか。 */
export function checkGate(branch, issueLabels) {
  const b = parseBranch(branch);
  const label = b && REQUIRED_GATE[b.role];
  if (!label) return [];
  if (issueLabels === null) return [issue('', 0, `Issue #${b.issue} が見つからない(ブランチ名の番号を確かめる)`)];
  return issueLabels.includes(label)
    ? []
    : [issue('', 0, `Issue #${b.issue} に ${label} が付いていない。${b.role}/ の工程は、前のゲートを人が承認してから始める`)];
}

/** 工場の版を上げる対象のファイル。 */
const FACTORY_FILES = ['.claude/', 'AGENTS.md', 'CLAUDE.md', '.github/workflows/', 'tools/factory/'];

/** factory-version: 工場の設定を変えたら、.factory/CHANGELOG.md も変えたか。 */
export function checkFactoryVersion(branch, changedFiles) {
  if (parseBranch(branch)?.role === 'dependabot') return [];
  const touched = changedFiles.filter((f) => FACTORY_FILES.some((p) => f.startsWith(p)));
  if (touched.length === 0 || changedFiles.includes('.factory/CHANGELOG.md')) return [];
  return [issue('.factory/CHANGELOG.md', 0, `工場の設定(${touched.slice(0, 3).join('、')}${touched.length > 3 ? ' ほか' : ''})を変えたので、CHANGELOG に 1 行足す(種類 A〜D)`)];
}
