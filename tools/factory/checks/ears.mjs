// ears-lint: 要件のファイルの frontmatter と、要件文が EARS の文型の 1 文 1 応答かを検査する(requirements/README.md)。
import { posix } from 'node:path';
import { AREAS, issue, readFrontmatter, requirementSentence } from '../lib/common.mjs';

const ID_RE = /^FR-(\d+)\.(\d+)([a-z])?(-\d+)?$/;
const SUBJECT = 'システムは、';
const PRIORITIES = ['Must', 'Should', 'Could'];
const STATUSES = ['有効', '廃止'];

/** 要件文 1 つを検査し、合わない理由の一覧を返す(合えば空)。 */
export function lintSentence(s) {
  const errors = [];
  if (!s.endsWith('。')) errors.push('文の終わりに「。」がない');
  if ((s.match(/。/g) ?? []).length > 1) errors.push('1 文にする(「。」が 2 つ以上ある)');
  const at = s.indexOf(SUBJECT);
  if (at < 0) return [...errors, '主語「システムは、」がない'];
  if (s.indexOf(SUBJECT, at + 1) >= 0) errors.push('主語「システムは、」が 2 つある');

  const prefix = s.slice(0, at);
  if (prefix !== '') {
    if (!/(とき|間|ならば|を含む場合)、$/.test(prefix)) {
      errors.push('前置きが EARS の文型に合わない(「〜とき、」「〜間、」「もし〜ならば、」「〜を含む場合、」のどれかで終える)');
    }
    if (prefix.includes('ならば、') && !prefix.includes('もし')) errors.push('「ならば」の文型は「もし」で始める');
  }

  const response = s.slice(at + SUBJECT.length).replace(/。$/, '');
  if (response.trim() === '') errors.push('応答がない');
  if (response.includes('、')) errors.push('応答の中に読点「、」を置かない(応答は 1 つ。2 つあるなら要件を分ける)');
  if (/(かつ|および|及び|並びに|ならびに)/.test(response)) errors.push('応答が 2 つある(かつ・および・並びに)。要件を分ける');
  return errors;
}

/** 要件のファイル 1 つを検査する。 */
export function checkRequirementFile(file, text) {
  const out = [];
  const fm = readFrontmatter(text);
  if (!fm) return [issue(file, 1, 'frontmatter(--- で囲んだ YAML)がない')];
  if (fm.error) return [issue(file, 1, `frontmatter の YAML が読めない: ${fm.error}`)];
  const d = fm.data;

  const expectedId = posix.basename(file, '.md');
  const m = ID_RE.exec(d.id ?? '');
  if (typeof d.id !== 'string') out.push(issue(file, 1, 'id がない'));
  else if (!m) out.push(issue(file, 1, `id の形が違う: ${d.id}(FR-<章>.<番号>、枝番は -1・-2)`));
  else {
    if (d.id !== expectedId) out.push(issue(file, 1, `id(${d.id})とファイル名(${expectedId})が違う`));
    const area = AREAS[Number(m[1])];
    const dir = file.split('/')[1];
    if (!area) out.push(issue(file, 1, `章 FR-${m[1]} の領域が決まっていない(requirements/README.md の表に足す)`));
    else if (dir !== area) out.push(issue(file, 1, `FR-${m[1]} は requirements/${area}/ に置く(いまは ${dir}/)`));
  }
  if (typeof d.title !== 'string' || d.title.trim() === '') out.push(issue(file, 1, 'title がない'));
  if (!PRIORITIES.includes(d.priority)) out.push(issue(file, 1, `priority は ${PRIORITIES.join(' | ')} のどれか`));
  if (!STATUSES.includes(d.status)) out.push(issue(file, 1, `status は ${STATUSES.join(' | ')} のどれか`));
  for (const key of ['business_rules', 'screens', 'issues']) {
    if (!Array.isArray(d[key])) out.push(issue(file, 1, `${key} は配列で書く(なければ [])`));
  }

  const { sentence, lines, line } = requirementSentence(fm);
  if (lines.length === 0) out.push(issue(file, line, '「## 要件」の節に要件文がない'));
  else if (lines.length > 1) out.push(issue(file, line, '「## 要件」の節には要件文を 1 行だけ書く'));
  else for (const e of lintSentence(sentence)) out.push(issue(file, line, `要件文: ${e}`));
  return out;
}
