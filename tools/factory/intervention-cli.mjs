#!/usr/bin/env node
// 介入の記録のコメントを確かめ、Issue・PR に intervention ラベルを付ける(.github/workflows/factory-intervention.yml)。
// 環境変数: BODY(コメントの本文)、NUMBER(Issue・PR の番号)、GITHUB_REPOSITORY、GH_TOKEN
import { execFileSync } from 'node:child_process';
import { USAGE, parseIntervention } from './lib/intervention.mjs';

const { BODY, NUMBER, GITHUB_REPOSITORY: repo } = process.env;
const gh = (...a) => execFileSync('gh', [...a, '--repo', repo], { encoding: 'utf8' });

const p = parseIntervention(BODY);
if (p === null) process.exit(0);
if (!p.ok) {
  gh('issue', 'comment', NUMBER, '--body', `介入の記録の形が違うので、数えていません: ${p.error}\n\n\`\`\`\n${USAGE}\n\`\`\``);
  console.log(`形が違う: ${p.error}`);
  process.exit(0);
}
gh('issue', 'edit', NUMBER, '--add-label', 'intervention');
console.log(`介入を記録しました: ${p.kind}・${p.cause}`);
if (p.kind === '手直し') {
  const url = `https://github.com/${repo}/issues/new?template=factory-improvement.yml`;
  gh('issue', 'comment', NUMBER, '--body', `成果物を手で直したので、原因(${p.cause})の側を直す Issue を[「工場の改善」](${url})で起票してください(AGENTS.md「手で直さない」)。`);
}
