#!/usr/bin/env node
// 工場の週報を作る(導入計画 §6)。リポジトリ直下で実行する。gh の認証が要る。
//   node tools/factory/metrics-cli.mjs                 週報を標準出力に出す(直近 7 日)
//   node tools/factory/metrics-cli.mjs --days 14       集計する期間を変える
//   node tools/factory/metrics-cli.mjs --post          「工場の週報」Issue(factory-report ラベル)にコメントで足す。なければ作る
// .github/workflows/factory-metrics.yml が毎週月曜 09:00 JST に --post で動かす。
import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { computeReport, reportMarkdown } from './lib/metrics.mjs';

const args = process.argv.slice(2);
const windowDays = Number(args[args.indexOf('--days') + 1]) || 7;
const post = args.includes('--post');

const gh = (...a) => execFileSync('gh', a, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }).trim();
/** gh api --paginate --jq で 1 行 1 JSON を返させ、配列にする。 */
const ghLines = (path, jq) =>
  gh('api', '--paginate', path, '--jq', jq)
    .split('\n')
    .filter(Boolean)
    .map((l) => JSON.parse(l));

const repo = process.env.GITHUB_REPOSITORY || gh('repo', 'view', '--json', 'nameWithOwner', '--jq', '.nameWithOwner');
const now = new Date();
const since = new Date(now.getTime() - windowDays * 24 * 60 * 60 * 1000);

const issues = ghLines(
  `repos/${repo}/issues?state=all&per_page=100`,
  '.[] | select(.pull_request | not) | {number, title, state, createdAt: .created_at, closedAt: .closed_at, labels: [.labels[].name]}',
).map((i) => ({
  ...i,
  labelEvents: ghLines(
    `repos/${repo}/issues/${i.number}/events?per_page=100`,
    '.[] | select(.event == "labeled" or .event == "unlabeled") | {label: .label.name, action: .event, at: .created_at}',
  ),
}));
const pulls = ghLines(
  `repos/${repo}/pulls?state=closed&per_page=100`,
  '.[] | select(.merged_at != null) | {number, headRef: .head.ref, mergedAt: .merged_at}',
);
// 介入の記録として数えるのは、リポジトリの関係者のコメントだけ(public リポジトリなので)
const comments = ghLines(
  `repos/${repo}/issues/comments?since=${since.toISOString()}&per_page=100`,
  '.[] | select(.author_association == "OWNER" or .author_association == "MEMBER" or .author_association == "COLLABORATOR")' +
    ' | {issueNumber: (.issue_url | split("/") | last | tonumber), body, createdAt: .created_at, author: .user.login, url: .html_url}',
);
const openQuestionsText = readFileSync('knowledge/open-questions.md', 'utf8');

const markdown = reportMarkdown(computeReport({ now, windowDays, issues, pulls, comments, openQuestionsText, repo }));
console.log(markdown);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${markdown}\n`);

if (post) {
  let number = gh('issue', 'list', '--repo', repo, '--label', 'factory-report', '--state', 'open', '--json', 'number', '--jq', '.[0].number // empty');
  if (!number) {
    const url = gh(
      'issue', 'create', '--repo', repo, '--title', '工場の週報', '--label', 'factory-report',
      '--body', '工場の計測値(.github/workflows/factory-metrics.yml が毎週月曜 09:00 JST にコメントで足す)。閉じないこと。',
    );
    number = url.split('/').pop();
  }
  const file = join(tmpdir(), `factory-report-${Date.now()}.md`);
  writeFileSync(file, markdown);
  gh('issue', 'comment', number, '--repo', repo, '--body-file', file);
  console.error(`週報を #${number} に足しました`);
}
