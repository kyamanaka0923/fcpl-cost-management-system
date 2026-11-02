#!/usr/bin/env node
// 工場のラインで、Issue のラベルから進めてよい工程を選ぶ(tools/factory/takt.sh が使う)。
// 使い方: node tools/factory/line-cli.mjs <Issue番号> [knowledge|req|spec|impl]
//   工程を渡したときは、ラベルから選んだ工程と同じかを確かめる(違えば進めない)。
// 出力(標準出力。シェルで eval できる形): ROLE・WORKFLOW・STAGE_NAME・STAGE_LABEL
// 進めないときは理由を標準エラーに出して、終了コード 1。
import { execFileSync } from 'node:child_process';
import { STAGES, selectStage } from './lib/gate-labels.mjs';

const [n, want] = process.argv.slice(2);
if (!/^\d+$/.test(n ?? '') || (want !== undefined && !(want in STAGES))) {
  console.error('使い方: node tools/factory/line-cli.mjs <Issue番号> [knowledge|req|spec|impl]');
  process.exit(2);
}

let issue;
try {
  issue = JSON.parse(execFileSync('gh', ['issue', 'view', n, '--json', 'state,title,labels'], { encoding: 'utf8' }));
} catch (e) {
  console.error(`Issue #${n} を読めない: ${e.message}`);
  process.exit(1);
}
const labels = issue.labels.map((l) => l.name);
const stop = (reason) => {
  console.error(`Issue #${n}「${issue.title}」は進めない: ${reason}`);
  console.error(`ラベル: ${labels.join('、') || 'なし'}`);
  process.exit(1);
};

if (issue.state !== 'OPEN') stop('open でない');
const picked = selectStage(labels);
if (picked.role === null) stop(picked.reason);
if (want !== undefined && want !== picked.role) stop(`ラベルから選べる工程は ${picked.role}(${STAGES[picked.role].name})で、指定の ${want} と違う`);

const stageLabel = labels.find((l) => l.startsWith('stage:'));
const q = (v) => `'${String(v).replaceAll("'", "'\\''")}'`;
console.log(`ROLE=${q(picked.role)}`);
console.log(`WORKFLOW=${q(STAGES[picked.role].workflow)}`);
console.log(`STAGE_NAME=${q(STAGES[picked.role].name)}`);
console.log(`STAGE_LABEL=${q(stageLabel)}`);
