// closing-keywords: impl/・factory/ 以外の PR の本文に、GitHub が「マージで閉じる」と読む書き方がないかを検査する(#57)。
// GitHub は否定の文を読まない。「Closes #46 は付けない」でも、マージすると #46 が閉じる。
import { issue, parseBranch } from '../lib/common.mjs';

/** 作業項目を閉じてよい工程。impl/ は作業項目の最後の工程、factory/ は工場の改善の Issue を閉じる */
const MAY_CLOSE = new Set(['impl', 'factory', 'dependabot']);

/** GitHub の閉じるキーワード(close・fix・resolve の活用形)+ Issue の参照(#n・owner/repo#n・Issue の URL) */
const CLOSING = /\b(close[sd]?|fix(?:e[sd])?|resolve[sd]?)\b:?\s+((?:[\w.-]+\/[\w.-]+)?#\d+|https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/issues\/\d+)/gi;

/** 本文の中の閉じる書き方を、行番号つきで返す。 */
export function findClosingKeywords(body) {
  const out = [];
  String(body ?? '')
    .split(/\r?\n/)
    .forEach((line, i) => {
      for (const m of line.matchAll(CLOSING)) out.push({ line: i + 1, text: m[0] });
    });
  return out;
}

export function checkClosingKeywords(branch, body) {
  const b = parseBranch(branch);
  if (!b || MAY_CLOSE.has(b.role)) return [];
  return findClosingKeywords(body).map(({ line, text }) =>
    issue('', 0, `PR の本文 ${line} 行目「${text}」: ${b.role}/ の PR では作業項目を閉じない。GitHub は否定の文を読まないので、閉じるキーワード(close・fix・resolve)を書かずに「Refs #番号」と書く`),
  );
}
