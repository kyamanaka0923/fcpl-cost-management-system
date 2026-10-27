// 工場の変更の記録(.factory/changes/<Issue番号>.md)から、一覧の表を作る(#66)。
import { readFrontmatter } from './common.mjs';

/**
 * 変更の記録の一覧を Markdown の表にする。Issue の番号の順(同じ番号はファイル名の順)。
 * frontmatter が読めないファイルは飛ばす(指摘は checkChangeFile が出す)。
 * @param {{file: string, text: string}[]} files
 */
export function changesMarkdown(files) {
  const rows = files
    .map(({ file, text }) => ({ file, fm: readFrontmatter(text) }))
    .filter(({ fm }) => fm && !fm.error)
    .map(({ file, fm }) => ({ file, issue: Number(fm.data.issue), kind: fm.data.kind, body: fm.body.trim().replace(/\s*\n\s*/g, ' ').replaceAll('|', '\\|') }))
    .sort((a, b) => a.issue - b.issue || a.file.localeCompare(b.file));
  if (rows.length === 0) return '変更の記録はまだない';
  return ['| Issue | 種類 | 中身 |', '|---|---|---|', ...rows.map((r) => `| #${r.issue} | ${r.kind} | ${r.body} |`)].join('\n');
}
