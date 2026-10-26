// 工場の週報の中身を計算する(導入計画 §6・§17.5・§17.6)。入力は GitHub から集めたデータで、ここでは通信しない。
import { CAUSES, KINDS, parseIntervention } from './intervention.mjs';

/** WIP の上限(導入計画 §17.5 の初期値)。 */
export const WIP_LIMITS = { judging: 5, openQuestions: 10, readyMin: 2 };

const DAY = 24 * 60 * 60 * 1000;
const GATES = ['gate0-approved', 'gate1-approved', 'gate2-approved'];
const ROLES = ['knowledge', 'req', 'spec', 'impl', 'prep', 'factory'];

/** 日時を JST の「2026-10-26 21:00」にする。 */
export function jst(date) {
  const d = new Date(new Date(date).getTime() + 9 * 60 * 60 * 1000);
  return d.toISOString().slice(0, 16).replace('T', ' ');
}

const days = (from, to) => (from && to ? (new Date(to) - new Date(from)) / DAY : null);
const fmtDays = (d) => (d === null ? '—' : `${d.toFixed(1)} 日`);

export function median(values) {
  const v = values.filter((x) => x !== null).sort((a, b) => a - b);
  if (v.length === 0) return null;
  const mid = Math.floor(v.length / 2);
  return v.length % 2 ? v[mid] : (v[mid - 1] + v[mid]) / 2;
}

/** open-questions.md の表で、回答が空の問いを数える。 */
export function countOpenQuestions(text) {
  return String(text ?? '')
    .split(/\r?\n/)
    .filter((l) => /^\|\s*OQ-\d+/.test(l))
    .filter((l) => {
      const cells = l.split('|').slice(1, -1).map((c) => c.trim());
      return (cells[6] ?? '') === '';
    }).length;
}

/** ラベルが最初に付いた日時(付いていなければ null)。 */
const firstLabeled = (issue, label) => issue.labelEvents.filter((e) => e.action === 'labeled' && e.label === label).map((e) => e.at).sort()[0] ?? null;

/** 工場のラインに乗っている作業項目か(工程・ゲートのラベルが一度でも付いた Issue)。 */
const isWorkItem = (issue) => [...issue.labels, ...issue.labelEvents.map((e) => e.label)].some((l) => l.startsWith('stage:') || GATES.includes(l));

/** 作業項目 1 件の、ゲートごとの通過日時と区間の日数。 */
export function flowOf(issue) {
  const t = [issue.createdAt, ...GATES.map((g) => firstLabeled(issue, g)), issue.state === 'closed' ? issue.closedAt : null];
  return {
    number: issue.number,
    title: issue.title,
    state: issue.state,
    toGate0: days(t[0], t[1]),
    toGate1: days(t[1], t[2]),
    toGate2: days(t[2], t[3]),
    toDone: days(t[3], t[4]),
    total: days(t[0], t[4]),
  };
}

/**
 * @param {object} input
 * @param {Date} input.now
 * @param {number} input.windowDays 集計する期間(日)
 * @param {{number, title, state, createdAt, closedAt, labels: string[], labelEvents: {label, action, at}[]}[]} input.issues PR を除く Issue
 * @param {{number, headRef, mergedAt}[]} input.pulls マージされた PR
 * @param {{issueNumber, body, createdAt, author, association, url}[]} input.comments 期間中のコメント
 * @param {string} input.openQuestionsText knowledge/open-questions.md
 * @param {string} input.repo
 */
export function computeReport({ now, windowDays, issues, pulls, comments, openQuestionsText, repo }) {
  const since = new Date(now.getTime() - windowDays * DAY);
  const inWindow = (at) => at && new Date(at) >= since && new Date(at) <= now;
  const open = issues.filter((i) => i.state === 'open');
  const work = issues.filter(isWorkItem);

  const wip = {
    judging: open.filter((i) => i.labels.includes('stage:判断待ち')).length,
    ready: open.filter((i) => i.labels.includes('stage:着手待ち')).length,
    needsAnswer: open.filter((i) => i.labels.includes('needs-answer')).length,
    openQuestions: countOpenQuestions(openQuestionsText),
  };
  const warnings = [];
  if (wip.judging > WIP_LIMITS.judging) warnings.push(`判断待ちが ${wip.judging} 件で、上限 ${WIP_LIMITS.judging} 件を超えた。新しい工程に着手せず prep の作業をする`);
  if (wip.openQuestions > WIP_LIMITS.openQuestions) warnings.push(`未回答の問いが ${wip.openQuestions} 件で、上限 ${WIP_LIMITS.openQuestions} 件を超えた`);
  if (wip.ready < WIP_LIMITS.readyMin) warnings.push(`着手待ちが ${wip.ready} 件で、${WIP_LIMITS.readyMin} 件を下回った。上流([A][B])を優先する`);

  const flows = work.map(flowOf);
  const done = flows.filter((f) => f.state === 'closed');
  const medians = {
    toGate0: median(done.map((f) => f.toGate0)),
    toGate1: median(done.map((f) => f.toGate1)),
    toGate2: median(done.map((f) => f.toGate2)),
    toDone: median(done.map((f) => f.toDone)),
    total: median(done.map((f) => f.total)),
  };
  const shown = work.filter((i) => i.state === 'open' || inWindow(i.closedAt)).map(flowOf);

  const merged = Object.fromEntries([...ROLES, 'その他'].map((r) => [r, 0]));
  for (const p of pulls.filter((p) => inWindow(p.mergedAt))) {
    const role = ROLES.find((r) => p.headRef.startsWith(`${r}/`)) ?? 'その他';
    merged[role]++;
  }
  const events = work.flatMap((i) => i.labelEvents.filter((e) => e.action === 'labeled' && inWindow(e.at)).map((e) => e.label));
  const counts = {
    needsAnswer: events.filter((l) => l === 'needs-answer').length,
    gates: Object.fromEntries(GATES.map((g) => [g, events.filter((l) => l === g).length])),
  };

  const interventions = [];
  const invalid = [];
  for (const c of comments.filter((c) => inWindow(c.createdAt))) {
    const p = parseIntervention(c.body);
    if (!p) continue;
    if (p.ok) interventions.push({ ...p, ...c });
    else invalid.push({ ...c, error: p.error });
  }

  return { since, now, windowDays, repo, wip, warnings, medians, shown, merged, counts, interventions, invalid };
}

/** 週報を Markdown にする。 */
export function reportMarkdown(r) {
  const L = [];
  L.push(`## 工場の週報 ${jst(r.since)} 〜 ${jst(r.now)}(JST)`, '');

  L.push('### WIP(仕掛かり)', '');
  L.push('| 指標 | 件数 | 上限 |', '|---|---|---|');
  L.push(`| 判断待ち(stage:判断待ち) | ${r.wip.judging} | ${WIP_LIMITS.judging} 件まで |`);
  L.push(`| 着手待ち(stage:着手待ち) | ${r.wip.ready} | ${WIP_LIMITS.readyMin} 件以上を保つ |`);
  L.push(`| 回答待ち(needs-answer) | ${r.wip.needsAnswer} | — |`);
  L.push(`| 未回答の問い(open-questions.md) | ${r.wip.openQuestions} | ${WIP_LIMITS.openQuestions} 件まで |`, '');
  L.push(...(r.warnings.length ? r.warnings.map((w) => `- ⚠️ ${w}`) : ['- 上限を超えたものはない']), '');

  L.push('### 流れの時間', '');
  L.push('完了した作業項目の中央値(起票 → ゲート0 → ゲート1 → ゲート2 → 完了)。ゲートを飛ばした区間は数えない。', '');
  L.push('| 起票→ゲート0 | ゲート0→1 | ゲート1→2 | ゲート2→完了 | 起票→完了 |', '|---|---|---|---|---|');
  const m = r.medians;
  L.push(`| ${fmtDays(m.toGate0)} | ${fmtDays(m.toGate1)} | ${fmtDays(m.toGate2)} | ${fmtDays(m.toDone)} | ${fmtDays(m.total)} |`, '');
  if (r.shown.length) {
    L.push('仕掛かり中と、期間中に完了した作業項目:', '');
    L.push('| Issue | 状態 | 起票→ゲート0 | ゲート0→1 | ゲート1→2 | ゲート2→完了 |', '|---|---|---|---|---|---|');
    for (const f of r.shown.sort((a, b) => a.number - b.number)) {
      L.push(`| #${f.number} ${f.title} | ${f.state === 'open' ? '仕掛かり' : '完了'} | ${fmtDays(f.toGate0)} | ${fmtDays(f.toGate1)} | ${fmtDays(f.toGate2)} | ${fmtDays(f.toDone)} |`);
    }
    L.push('');
  }

  L.push('### 期間中のできごと', '');
  L.push(`- マージした PR: ${Object.entries(r.merged).filter(([, n]) => n).map(([k, n]) => `${k} ${n}`).join('、') || 'なし'}`);
  L.push(`- ゲートの通過: ${Object.entries(r.counts.gates).map(([g, n]) => `${g} ${n}`).join('、')}`);
  L.push(`- needs-answer を付けた回数: ${r.counts.needsAnswer}`, '');

  L.push(`### 介入(${r.interventions.length} 件)`, '');
  if (r.interventions.length) {
    L.push(`| 種類 \\ 原因 | ${CAUSES.join(' | ')} |`, `|---|${CAUSES.map(() => '---').join('|')}|`);
    for (const k of KINDS) L.push(`| ${k} | ${CAUSES.map((c) => r.interventions.filter((i) => i.kind === k && i.cause === c).length).join(' | ')} |`);
    L.push('');
    for (const i of r.interventions) L.push(`- [#${i.issueNumber}](${i.url}) ${i.kind}・${i.cause}${i.note ? `: ${i.note}` : ''}`);
    L.push('');
    const handFixes = r.interventions.filter((i) => i.kind === '手直し').length;
    if (handFixes) L.push(`- 手で直したのは ${handFixes} 件。原因を直す「工場の改善」の Issue を起票したかを確かめる`, '');
  } else {
    L.push('- なし', '');
  }
  if (r.invalid.length) {
    L.push('形の違う介入の記録(数えていない):', '');
    for (const i of r.invalid) L.push(`- [#${i.issueNumber}](${i.url}) ${i.error}`);
    L.push('');
  }

  L.push('### 手で記入する', '');
  L.push('- 費用(Claude の利用量): ');
  L.push(`- ミューテーションスコア: [main の CI](https://github.com/${r.repo}/actions/workflows/ci.yml?query=branch%3Amain) の「ミューテーションテスト」の実行サマリ`, '');
  return L.join('\n');
}
