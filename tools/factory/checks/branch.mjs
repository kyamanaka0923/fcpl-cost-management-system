// PR のブランチに対する検査: role-guard(持ち場)・gate-check(ゲートラベル)・factory-version(工場の版)。
import { issue, parseBranch, readFrontmatter } from '../lib/common.mjs';
import { REQUIRED_GATES, checkGateEvidence } from '../lib/gate-labels.mjs';

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

/**
 * gate-check: 着手するのに要るゲートを、Issue がすべて通っているか(#77)。
 * 直前のゲートだけでなく、その工程までのゲートラベルをすべて求め、ゲートラベルの裏付け(その工程の PR がマージ済みか)も見る。
 * @param {string[] | null} issueLabels Issue のラベル(Issue が見つからなければ null)
 * @param {string[] | null} mergedBranches main にマージされた PR の head のブランチ名(取れなければ null)
 */
export function checkGate(branch, issueLabels, mergedBranches = []) {
  const b = parseBranch(branch);
  const required = b && REQUIRED_GATES[b.role];
  if (!required) return [];
  if (issueLabels === null) return [issue('', 0, `Issue #${b.issue} が見つからない(ブランチ名の番号を確かめる)`)];
  const missing = required.filter((g) => !issueLabels.includes(g));
  const errors = missing.length
    ? [issue('', 0, `Issue #${b.issue} に ${missing.join('・')} が付いていない。${b.role}/ の工程は、${required.join('・')} がすべてそろってから始める(ゲートを飛ばさない)`)]
    : [];
  if (mergedBranches === null) return [...errors, issue('', 0, 'マージ済みの PR の一覧を取れなかったので、ゲートラベルの裏付けを確かめられない')];
  return [...errors, ...checkGateEvidence(b.issue, issueLabels, mergedBranches).map((m) => issue('', 0, `Issue #${b.issue}: ${m}`))];
}

/** 工場の版を上げる対象のファイル。 */
const FACTORY_FILES = ['.claude/', 'AGENTS.md', 'CLAUDE.md', '.github/workflows/', 'tools/factory/'];

/** 変更の記録のファイル(.factory/changes/<Issue番号>.md。1 つの Issue で 2 つ目の PR を出すときは <Issue番号>-<名前>.md)。 */
export const CHANGE_FILE = /^\.factory\/changes\/(\d+)(?:-[^/]+)?\.md$/;

/** 種類(.factory/CHANGELOG.md の「変更の種類」)。 */
const KINDS = ['A', 'B', 'C', 'D'];

/**
 * 変更の記録 1 件を確かめる。frontmatter の issue がファイル名の番号と同じで、kind が A〜D、本文に中身があるか。
 * @param {string} file リポジトリ直下からのパス
 * @param {string | null} text ファイルの中身(消したファイルは null)
 */
export function checkChangeFile(file, text) {
  if (text === null) return [issue(file, 0, '変更の記録は消さない(マージ済みの変更の記録になっている)')];
  const fm = readFrontmatter(text);
  if (!fm) return [issue(file, 1, 'frontmatter(issue と kind)がない')];
  if (fm.error) return [issue(file, 1, `frontmatter が読めない: ${fm.error}`)];
  const errors = [];
  const n = Number(CHANGE_FILE.exec(file)[1]);
  if (fm.data.issue !== n) errors.push(issue(file, 1, `issue が ${fm.data.issue ?? 'ない'}。ファイル名の番号 ${n} と同じにする`));
  if (!KINDS.includes(fm.data.kind)) errors.push(issue(file, 1, `kind が ${fm.data.kind ?? 'ない'}。A〜D のどれかにする(.factory/CHANGELOG.md の「変更の種類」)`));
  if (fm.body.trim() === '') errors.push(issue(file, fm.bodyStartLine, '本文に、何を変えたかを 1〜2 文で書く'));
  return errors;
}

/**
 * factory-version: 工場の設定を変えたら、変更の記録(.factory/changes/)を足したか。
 * 記録を 1 件 1 ファイルにして、工場の PR が同時に開いても同じ場所に行を足さない(衝突しない)ようにしている(#66)。
 * @param {(path: string) => string | null} readChanged PR の head のファイルの中身(消したファイルは null)
 */
export function checkFactoryVersion(branch, changedFiles, readChanged = () => null) {
  if (parseBranch(branch)?.role === 'dependabot') return [];
  const changes = changedFiles.filter((f) => CHANGE_FILE.test(f));
  const errors = changes.flatMap((f) => checkChangeFile(f, readChanged(f)));
  const touched = changedFiles.filter((f) => FACTORY_FILES.some((p) => f.startsWith(p)));
  if (touched.length > 0 && changes.length === 0) {
    errors.push(
      issue(
        '.factory/changes/',
        0,
        `工場の設定(${touched.slice(0, 3).join('、')}${touched.length > 3 ? ' ほか' : ''})を変えたので、変更の記録 .factory/changes/<Issue番号>.md を足す(種類 A〜D。書き方は .factory/changes/README.md)`,
      ),
    );
  }
  return errors;
}
