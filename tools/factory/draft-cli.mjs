#!/usr/bin/env node
// takt が異常終了したときの書きかけの扱い(#101)。tools/factory/takt.sh が使う。リポジトリ直下で実行する。
//   node tools/factory/draft-cli.mjs files <inside|outside> <ブランチ>
//     作業ツリーの変更のうち、ブランチの持ち場の中(inside)か外(outside)のファイルを、NUL 区切りで出す
//   node tools/factory/draft-cli.mjs run <開始時刻(ミリ秒)>   (タスクの本文は環境変数 FACTORY_TASK)
//     この実行の takt の run の状態と、止まった理由を「状態<TAB>理由」で出す。run がなければ「unknown<TAB>」
import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { findRun, parseStatusZ, splitByRole } from './lib/draft.mjs';

const [command, ...args] = process.argv.slice(2);

if (command === 'files') {
  const [which, branch] = args;
  const status = execFileSync('git', ['status', '--porcelain=v1', '-z', '-uall', '--no-renames'], { encoding: 'utf8' });
  const split = splitByRole(branch, parseStatusZ(status));
  const files = which === 'inside' ? split.inside : split.outside;
  process.stdout.write(files.map((f) => `${f}\0`).join(''));
} else if (command === 'run') {
  const runsDir = '.takt/runs';
  const metas = existsSync(runsDir)
    ? readdirSync(runsDir)
        .map((d) => join(runsDir, d))
        .filter((d) => existsSync(join(d, 'meta.json')))
        .map((dir) => ({ dir, ...JSON.parse(readFileSync(join(dir, 'meta.json'), 'utf8')) }))
    : [];
  const run = findRun(metas, process.env.FACTORY_TASK ?? '', Number(args[0]));
  if (!run) {
    console.log('unknown\t');
  } else {
    // 止まった理由は、セッションのログの workflow_abort に書かれる
    const logs = join(run.dir, 'logs');
    const reason = existsSync(logs)
      ? readdirSync(logs)
          .filter((f) => f.endsWith('.jsonl') && !/(otel|usage)/.test(f))
          .flatMap((f) => readFileSync(join(logs, f), 'utf8').split('\n'))
          .filter(Boolean)
          .map((line) => JSON.parse(line))
          .find((e) => e.type === 'workflow_abort')?.reason ?? ''
      : '';
    console.log(`${run.status}\t${reason.replace(/\s+/g, ' ')}`);
  }
} else {
  console.error('使い方: draft-cli.mjs files <inside|outside> <ブランチ> | run <開始時刻(ミリ秒)>');
  process.exit(2);
}
