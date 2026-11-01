#!/usr/bin/env node
// PR で変えた場所から、動かす CI のジョブを決めて GITHUB_OUTPUT に書く(#73)。リポジトリ直下で実行する。
//   FACTORY_BASE / FACTORY_HEAD  PR の base と head のコミット。どちらかが空なら(main への push など)すべて動かす
// 依存のパッケージを使わないので、npm ci なしで動く。
import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { ciScope } from './lib/ci-scope.mjs';

const { FACTORY_BASE: base, FACTORY_HEAD: head, GITHUB_OUTPUT: out, GITHUB_STEP_SUMMARY: summary } = process.env;

let files = null;
if (base && head) {
  try {
    const diff = execFileSync('git', ['-c', 'core.quotepath=false', 'diff', '--name-only', '--no-renames', `${base}...${head}`], { encoding: 'utf8' }).trim();
    files = diff ? diff.split('\n') : [];
  } catch (e) {
    console.log(`差分を取れなかったので、すべてのジョブを動かす: ${e.message}`);
  }
}

const scope = ciScope(files);
const lines = Object.entries(scope).map(([k, v]) => `${k}=${v}`);
console.log(files === null ? 'PR ではない(または差分なし)ので、すべてのジョブを動かす' : `変えたファイル ${files.length} 件`);
console.log(lines.join('\n'));
if (out) appendFileSync(out, `${lines.join('\n')}\n`);
if (summary) {
  const rows = Object.entries(scope).map(([k, v]) => `| ${k} | ${v ? '動かす' : '飛ばす'} |`).join('\n');
  appendFileSync(summary, `## 動かす CI のジョブ(#73)\n\n| ジョブ | |\n|---|---|\n${rows}\n`);
}
