// knowledge-check: 業務ルールに出どころがあり、要件が確定した業務ルールだけを参照しているかを検査する(knowledge/README.md)。
import { posix } from 'node:path';
import { issue, readFrontmatter } from '../lib/common.mjs';

const ID_RE = /^BR-[A-Z]+-\d{3}$/;
const STATUSES = ['確定', '未確定'];

/** 業務ルールのファイル 1 つを検査する。 */
export function checkBusinessRule(file, text, sourceIds) {
  const fm = readFrontmatter(text);
  if (!fm) return [issue(file, 1, 'frontmatter(--- で囲んだ YAML)がない')];
  if (fm.error) return [issue(file, 1, `frontmatter の YAML が読めない: ${fm.error}`)];
  const d = fm.data;
  const out = [];
  const expectedId = posix.basename(file, '.md');
  if (!ID_RE.test(d.id ?? '')) out.push(issue(file, 1, `id の形が違う: ${d.id}(BR-<領域>-<3 桁>)`));
  else if (d.id !== expectedId) out.push(issue(file, 1, `id(${d.id})とファイル名(${expectedId})が違う`));
  if (typeof d.title !== 'string' || d.title.trim() === '') out.push(issue(file, 1, 'title がない'));
  if (!STATUSES.includes(d.status)) out.push(issue(file, 1, `status は ${STATUSES.join(' | ')} のどれか`));
  if (!Array.isArray(d.sources) || d.sources.length === 0) {
    out.push(issue(file, 1, 'sources(出どころ)がない。knowledge/sources.md の ID を 1 つ以上書く'));
  } else {
    for (const s of d.sources) if (!sourceIds.has(s)) out.push(issue(file, 1, `出どころ ${s} が knowledge/sources.md にない`));
  }
  if (d.requirements !== undefined && !Array.isArray(d.requirements)) out.push(issue(file, 1, 'requirements は配列で書く'));
  return out;
}

/** 要件が参照する業務ルールが、存在して確定しているかを検査する。 */
export function checkRequirementRules(requirements, businessRules) {
  const out = [];
  for (const [id, req] of requirements) {
    for (const br of req.data.business_rules ?? []) {
      const rule = businessRules.get(br);
      if (!rule) out.push(issue(req.file, 1, `${id} が参照する業務ルール ${br} がない`));
      else if (rule.data.status !== '確定') out.push(issue(req.file, 1, `${id} が未確定の業務ルール ${br} を参照している(先にゲート0で確定させる)`));
    }
  }
  return out;
}
