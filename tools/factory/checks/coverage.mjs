// coverage-audit: 要件とシナリオのトレーサビリティを作り、網羅の穴と孤児のタグを検査する(requirements/README.md「受け入れ条件」)。
import { issue } from '../lib/common.mjs';

/** まだ実装のないシナリオの印(要件の工程で付け、実装の工程で外す。#60) */
export const WIP_TAG = '未実装';

/**
 * @param {Map<string, {file, data, sentence}>} requirements 移した要件
 * @param {Set<string>} businessRuleIds 業務ルールの ID
 * @param {{file: string, parsed: ReturnType<import('../lib/gherkin.mjs').parseFeature>}[]} features
 * @param {{implIssue?: number}} [options] implIssue: impl/<n> の PR の Issue 番号。その Issue の要件を指すシナリオに @未実装 が残っていたら落とす
 * @returns {{errors: object[], matrix: {id, title, status, scenarios: {file, line, title, wip}[]}[]}}
 */
export function audit(requirements, businessRuleIds, features, { implIssue } = {}) {
  const errors = [];
  const hits = new Map([...requirements.keys()].map((id) => [id, []]));
  const reported = new Set();
  const once = (key, e) => {
    if (!reported.has(key)) errors.push(e), reported.add(key);
  };

  for (const { file, parsed } of features) {
    for (const rule of parsed.rules) {
      const frs = rule.tags.filter((t) => t.startsWith('FR-'));
      if (frs.length > 1) errors.push(issue(file, rule.line, `1 つの「ルール:」には 1 つの要件(いまは ${frs.join('・')})`));
      if (frs.length === 1 && requirements.has(frs[0])) {
        const req = requirements.get(frs[0]);
        if (req.sentence && rule.title !== req.sentence) {
          errors.push(issue(file, rule.line, `「ルール:」の文が ${frs[0]} の要件文と違う。要件文をそのまま書く: ${req.sentence}`));
        }
      }
      if (frs.length === 1 && rule.scenarios.length === 0) errors.push(issue(file, rule.line, `${frs[0]} の「ルール:」の下にシナリオがない`));
    }
    for (const sc of parsed.scenarios) {
      for (const tag of new Set(sc.tags)) {
        if (tag.startsWith('FR-')) {
          const req = requirements.get(tag);
          if (!req) once(`${file}:${tag}`, issue(file, sc.line, `@${tag} は requirements/ にない要件を指している(孤児のタグ)`));
          else if (req.data.status === '廃止') once(`${file}:${tag}`, issue(file, sc.line, `@${tag} は廃止した要件を指している`));
          else {
            const wip = sc.tags.includes(WIP_TAG);
            hits.get(tag).push({ file, line: sc.line, title: sc.title, wip });
            if (wip && implIssue !== undefined && (req.data.issues ?? []).includes(implIssue)) {
              errors.push(issue(file, sc.line, `${tag} は Issue #${implIssue} の要件。実装の PR では、このシナリオの @${WIP_TAG} を外して通す`));
            }
          }
        } else if (tag.startsWith('BR-') && !businessRuleIds.has(tag)) {
          once(`${file}:${tag}`, issue(file, sc.line, `@${tag} は knowledge/business-rules/ にない業務ルールを指している`));
        }
      }
    }
  }

  const matrix = [];
  for (const [id, req] of requirements) {
    const scenarios = hits.get(id);
    matrix.push({ id, title: req.data.title, status: req.data.status, scenarios });
    if (req.data.status !== '廃止' && scenarios.length === 0) {
      errors.push(issue(req.file, 1, `${id} を確かめるシナリオがない(.feature の「ルール:」に @${id} を付ける)`));
    }
  }
  matrix.sort((a, b) => a.id.localeCompare(b.id, 'ja', { numeric: true }));
  return { errors, matrix };
}

/** トレーサビリティマトリクスを Markdown にする。 */
export function matrixMarkdown(matrix) {
  if (matrix.length === 0) return '移した要件はまだない(requirements/ に FR-*.md がない)。\n';
  const rows = matrix.map((r) => {
    const where = r.scenarios.map((s) => `${s.file}:${s.line}${s.wip ? '(未実装)' : ''}`).join('<br>') || '**なし**';
    const wip = r.scenarios.filter((s) => s.wip).length;
    return `| ${r.id} | ${r.title} | ${r.status} | ${r.scenarios.length} | ${wip} | ${where} |`;
  });
  return ['| 要件 | 題 | 状態 | シナリオ数 | うち未実装 | シナリオ |', '|---|---|---|---|---|---|', ...rows].join('\n') + '\n';
}
