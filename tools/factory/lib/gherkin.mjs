// 日本語 Gherkin(# language: ja)の .feature から、ルールとシナリオと、それぞれに効くタグを読む。
// 網羅監査に要る分だけを読む簡易なパーサ(ステップ・表・DocString の中身は見ない)。

const KEYWORD = {
  feature: /^機能[:：]\s*(.*)$/,
  rule: /^ルール[:：]\s*(.*)$/,
  scenario: /^(?:シナリオアウトライン|シナリオテンプレート|シナリオ)[:：]\s*(.*)$/,
  other: /^(?:背景|例|サンプル)[:：]/,
};

/**
 * @returns {{rules: {title: string, line: number, tags: string[], scenarios: object[]}[],
 *            scenarios: {title: string, line: number, tags: string[], rule: object|null}[]}}
 *   scenarios[].tags は、機能・ルール・シナリオに付いたタグを合わせたもの(@ を除く)
 */
export function parseFeature(text) {
  const lines = text.split(/\r?\n/);
  const rules = [];
  const scenarios = [];
  let pending = [];
  let featureTags = [];
  let rule = null;
  let inDocString = false;

  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();
    if (t.startsWith('"""') || t.startsWith('```')) {
      inDocString = !inDocString;
      continue;
    }
    if (inDocString || t === '' || t.startsWith('#')) continue;
    if (t.startsWith('@')) {
      pending.push(...t.replace(/\s#.*$/, '').split(/\s+/).filter((x) => x.startsWith('@')).map((x) => x.slice(1)));
      continue;
    }
    let m;
    if ((m = KEYWORD.feature.exec(t))) {
      featureTags = pending;
    } else if ((m = KEYWORD.rule.exec(t))) {
      rule = { title: m[1].trim(), line: i + 1, tags: pending, scenarios: [] };
      rules.push(rule);
    } else if ((m = KEYWORD.scenario.exec(t))) {
      const sc = { title: m[1].trim(), line: i + 1, tags: [...featureTags, ...(rule?.tags ?? []), ...pending], rule };
      scenarios.push(sc);
      rule?.scenarios.push(sc);
    }
    // タグは直後のキーワードにだけ付く。ステップや表の行を挟んだら捨てる
    pending = [];
  }
  return { rules, scenarios };
}
