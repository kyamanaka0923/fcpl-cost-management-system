// upstream-approved: 前提の記録(.factory/premises/ISSUE-<n>.yml)の版が、main の版と一致するかを検査する。
import { parse as parseYaml } from 'yaml';
import { issue, parseBranch } from '../lib/common.mjs';

/**
 * @param {string} branch PR のブランチ
 * @param {string|null} premisesText 前提の記録の中身(なければ null)
 * @param {(path: string) => string|null} blobOnMain main にあるそのファイルの blob SHA(なければ null)
 */
export function checkPremises(branch, premisesText, blobOnMain) {
  const b = parseBranch(branch);
  if (!b || (b.role !== 'spec' && b.role !== 'impl')) return [];
  const file = `.factory/premises/ISSUE-${b.issue}.yml`;
  if (premisesText === null) {
    return [issue(file, 0, `前提の記録がない。bash tools/factory/premises.sh ${b.issue} ${b.role} <読んだ要件・仕様・業務ルール>... で作る`)];
  }
  let p;
  try {
    p = parseYaml(premisesText);
  } catch (e) {
    return [issue(file, 1, `YAML が読めない: ${e.message}`)];
  }
  const out = [];
  if (p?.issue !== b.issue) out.push(issue(file, 1, `issue(${p?.issue})がブランチの Issue 番号(${b.issue})と違う`));
  if (p?.stage !== b.role) out.push(issue(file, 1, `stage(${p?.stage})がブランチの工程(${b.role})と違う。この工程で書き直す`));
  if (!Array.isArray(p?.premises) || p.premises.length === 0) return [...out, issue(file, 1, 'premises が空。前提にしたファイルを書く')];
  for (const { path, blob } of p.premises) {
    const now = blobOnMain(path);
    if (now === null) out.push(issue(file, 0, `${path} が main にない(承認されていないものは前提にできない)`));
    else if (now !== blob) {
      out.push(issue(file, 0, `${path} が前提を書いたあとで main で変わった(${String(blob).slice(0, 7)} → ${now.slice(0, 7)})。作業を止めて Issue に needs-answer を付ける`));
    }
  }
  return out;
}
