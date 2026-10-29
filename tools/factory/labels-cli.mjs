#!/usr/bin/env node
// Issue のラベルを付け外ししたその場で、ゲートラベル・列のラベル・マージ済みの工程の PR の組み合わせを確かめる(#77)。
// 表にない組み合わせなら Issue にコメントで知らせる(ラベルは付け外ししない。人の仕事)。
// あわせて、その Issue の開いている工程の PR の「工場のゲート」の失敗したジョブを再実行する(ゲートラベルを付けたあとの手での再実行を減らす)。
// .github/workflows/factory-labels.yml が動かす。環境変数: NUMBER(Issue の番号)、GITHUB_REPOSITORY、GH_TOKEN
import { execFileSync } from 'node:child_process';
import { checkIssueLabels } from './lib/gate-labels.mjs';

const { NUMBER, GITHUB_REPOSITORY: repo } = process.env;
const n = Number(NUMBER);
const api = (...a) => execFileSync('gh', ['api', ...a], { encoding: 'utf8' });
const MARKER = '<!-- factory-labels -->';

const labels = JSON.parse(api(`repos/${repo}/issues/${n}`, '--jq', '[.labels[].name]'));
const merged = api('--paginate', `repos/${repo}/pulls?state=closed&base=main&per_page=100`, '--jq', '.[] | select(.merged_at) | .head.ref').split('\n').filter(Boolean);
const problems = checkIssueLabels(n, labels, merged);
console.log(`Issue #${n} のラベル: ${labels.join('、') || 'なし'}`);
console.log(problems.length ? problems.map((p) => `- ${p}`).join('\n') : '指摘 0 件');

// 前の知らせのコメントがあれば書き換え、なければ指摘があるときだけ足す(コメントを増やしすぎない)
const prev = api('--paginate', `repos/${repo}/issues/${n}/comments?per_page=100`, '--jq', `.[] | select(.body | startswith("${MARKER}")) | .id`).split('\n').filter(Boolean);
const body = problems.length
  ? `${MARKER}\nゲートラベル・列のラベルの組み合わせが、工場の表(\`factory-line\` スキルの「1.」)にありません。ラベルを確かめてください(この知らせはラベルを付け外ししません)。\n\n${problems.map((p) => `- ${p}`).join('\n')}\n\nラベル: ${labels.join('、')}`
  : `${MARKER}\nゲートラベル・列のラベルの組み合わせは、工場の表どおりになりました(前の知らせは解消)。`;
if (prev.length) api('-X', 'PATCH', `repos/${repo}/issues/comments/${prev[prev.length - 1]}`, '-f', `body=${body}`);
else if (problems.length) api(`repos/${repo}/issues/${n}/comments`, '-f', `body=${body}`);

// 開いている工程の PR の「工場のゲート」を、失敗したジョブだけ再実行する
const open = api('--paginate', `repos/${repo}/pulls?state=open&per_page=100`, '--jq', '.[] | .head.ref').split('\n').filter((r) => new RegExp(`^(req|spec|impl)/${n}-`).test(r));
for (const ref of open) {
  const run = api(`repos/${repo}/actions/workflows/factory.yml/runs?branch=${encodeURIComponent(ref)}&event=pull_request&per_page=1`, '--jq', '.workflow_runs[0] | "\\(.id) \\(.status) \\(.conclusion)"').trim();
  const [id, status, conclusion] = run.split(' ');
  if (id && status === 'completed' && conclusion === 'failure') {
    try {
      api('-X', 'POST', `repos/${repo}/actions/runs/${id}/rerun-failed-jobs`);
      console.log(`${ref} の工場のゲート(run ${id})の失敗したジョブを再実行した`);
    } catch (e) {
      console.log(`${ref} の工場のゲート(run ${id})を再実行できなかった: ${e.message}`);
    }
  }
}
