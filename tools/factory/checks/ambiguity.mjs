// ambiguity-check: 要件・業務ルール・.feature の「ルール:」行に、曖昧語がないかを検査する(tools/factory/ambiguity-words.txt)。
import { issue } from '../lib/common.mjs';

/** 検査する行か。.feature は「ルール:」行だけ、Markdown は全行。 */
const target = (file, line) => (file.endsWith('.feature') ? /^\s*ルール[:：]/.test(line) : true);

/** ファイル 1 つの中の曖昧語を探す。 */
export function findAmbiguousWords(file, text, words) {
  const out = [];
  text.split(/\r?\n/).forEach((line, i) => {
    if (!target(file, line)) return;
    for (const w of words) if (line.includes(w)) out.push(issue(file, i + 1, `曖昧語「${w}」を使っている(requirements/README.md「禁止語」)`));
  });
  return out;
}

/** 検査するファイルか(README は禁止語の説明を含むので除く)。 */
export const isAmbiguityTarget = (rel) =>
  (/^requirements\/.+\.md$/.test(rel) && !rel.endsWith('README.md')) ||
  /^knowledge\/business-rules\/.+\.md$/.test(rel) ||
  (/^tests\/.+\.feature$/.test(rel));
