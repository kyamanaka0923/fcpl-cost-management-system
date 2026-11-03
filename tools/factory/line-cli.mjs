#!/usr/bin/env node
// 工場のラインで、Issue のラベルから進めてよい工程を選ぶ(tools/factory/takt.sh が使う)。
// 使い方: node tools/factory/line-cli.mjs <Issue番号> [knowledge|req|spec|impl]
//   工程を渡したときは、ラベルから選んだ工程と同じかを確かめる(違えば進めない)。
// 出力(標準出力。シェルで eval できる形): ROLE・WORKFLOW・STAGE_NAME・STAGE_LABEL・RESTORE_STAGE・REDO
//   RESTORE_STAGE: 人が回答して needs-answer を外したとき、stage:判断待ち から戻す列(なければ空。付け替えは takt.sh がする。#102)
//   REDO: 人が redo:<工程> を付けて上流をやり直すときは 1(#102)
// 進めないときは理由を標準エラーに出して、終了コード 1。
import { execFileSync } from 'node:child_process';
import { STAGES, labelsAfterAnswer, selectStage } from './lib/gate-labels.mjs';

const [n, want] = process.argv.slice(2);
if (!/^\d+$/.test(n ?? '') || (want !== undefined && !(want in STAGES))) {
  console.error('使い方: node tools/factory/line-cli.mjs <Issue番号> [knowledge|req|spec|impl]');
  process.exit(2);
}

let issue;
try {
  issue = JSON.parse(execFileSync('gh', ['issue', 'view', n, '--json', 'state,title,labels,comments'], { encoding: 'utf8' }));
} catch (e) {
  console.error(`Issue #${n} を読めない: ${e.message}`);
  process.exit(1);
}
const current = issue.labels.map((l) => l.name);
const after = labelsAfterAnswer(current, issue.comments.map((c) => c.body));
const stop = (reason) => {
  console.error(`Issue #${n}「${issue.title}」は進めない: ${reason}`);
  console.error(`ラベル: ${current.join('、') || 'なし'}`);
  process.exit(1);
};

if (issue.state !== 'OPEN') stop('open でない');
if (after.error) stop(after.error);
const { labels, restore } = after;
const picked = selectStage(labels);
if (picked.role === null) stop(picked.reason);
if (want !== undefined && want !== picked.role) stop(`ラベルから選べる工程は ${picked.role}(${STAGES[picked.role].name})で、指定の ${want} と違う`);

const stageLabel = labels.find((l) => l.startsWith('stage:'));
const q = (v) => `'${String(v).replaceAll("'", "'\\''")}'`;
console.log(`ROLE=${q(picked.role)}`);
console.log(`WORKFLOW=${q(STAGES[picked.role].workflow)}`);
console.log(`STAGE_NAME=${q(STAGES[picked.role].name)}`);
console.log(`STAGE_LABEL=${q(stageLabel)}`);
console.log(`RESTORE_STAGE=${q(restore ?? '')}`);
console.log(`REDO=${q(picked.redo ? 1 : '')}`);
