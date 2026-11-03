// takt が異常終了したときの書きかけの扱い(#101)。tools/factory/takt.sh が draft-cli.mjs から使う。
import { checkRole } from '../checks/branch.mjs';

/**
 * `git status --porcelain=v1 -z -uall --no-renames` の出力から、変わったファイルのパスを取り出す。
 * -z なので、空白や日本語を含むパスも引用符なしで読める。
 */
export function parseStatusZ(text) {
  return text
    .split('\0')
    .filter((entry) => entry.length > 3)
    .map((entry) => entry.slice(3));
}

/**
 * 変わったファイルを、ブランチの工程の持ち場(ROLES)の中と外に分ける。
 * 中はコミットして書きかけとして残し、外は人が確かめるまで作業ツリーに残す。
 * @returns {{ inside: string[], outside: string[] }}
 */
export function splitByRole(branch, files) {
  const outside = new Set(checkRole(branch, files).map((e) => e.file));
  // ブランチ名が命名に合わないときは、checkRole がファイルを指さない指摘を返す。そのときは全部を外にする
  if (checkRole(branch, []).length > 0) return { inside: [], outside: [...files] };
  return { inside: files.filter((f) => !outside.has(f)), outside: files.filter((f) => outside.has(f)) };
}

/**
 * takt の run の結果を、meta.json とセッションのログから読む。
 * @param {{ task: string, status: string, startTime: string }[]} metas runs/ の meta.json(と run のディレクトリ)
 * @param {string} task この実行のタスクの本文
 * @param {number} since この実行を始めた時刻(ミリ秒)。同じタスクの前の run を拾わないため
 * @returns 見つからなければ null
 */
export function findRun(metas, task, since) {
  return (
    metas
      .filter((m) => m.task === task && Date.parse(m.startTime) >= since)
      .sort((a, b) => Date.parse(b.startTime) - Date.parse(a.startTime))[0] ?? null
  );
}
