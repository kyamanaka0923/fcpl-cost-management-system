// 工場の検査で共通に使う部品(ファイルの走査・frontmatter・指摘の形)。
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, posix } from 'node:path';
import { parse as parseYaml } from 'yaml';

/** 要件の章番号と領域のディレクトリ(requirements/README.md)。 */
export const AREAS = {
  1: 'organization',
  2: 'project',
  3: 'cost-element',
  4: 'budget',
  5: 'approval',
  6: 'actual',
  7: 'variance',
  8: 'profit',
  9: 'comparison',
  10: 'division',
};

/** 指摘 1 件。line は 1 始まり。 */
export const issue = (file, line, message) => ({ file, line, message });

/** root からの相対パス(/ 区切り)で、dir 以下のファイルを列挙する。 */
export function walk(root, dir, pred = () => true) {
  const out = [];
  const abs = join(root, dir);
  if (!existsSync(abs)) return out;
  for (const ent of readdirSync(abs, { withFileTypes: true })) {
    const rel = posix.join(dir, ent.name);
    if (ent.isDirectory()) {
      if (ent.name === 'node_modules' || ent.name === 'bin' || ent.name === 'obj') continue;
      out.push(...walk(root, rel, pred));
    } else if (pred(rel)) {
      out.push(rel);
    }
  }
  return out.sort();
}

export const read = (root, rel) => readFileSync(join(root, rel), 'utf8');

/**
 * 先頭の YAML frontmatter を読む。
 * @returns {{data: object, body: string, bodyStartLine: number} | {error: string} | null}
 */
export function readFrontmatter(text) {
  const m = /^---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(text);
  if (!m) return null;
  let data;
  try {
    data = parseYaml(m[1]) ?? {};
  } catch (e) {
    return { error: e.message };
  }
  return { data, body: text.slice(m[0].length), bodyStartLine: m[0].split('\n').length };
}

/** 要件のファイル(requirements/<領域>/FR-*.md)か。 */
export const isRequirementFile = (rel) => /^requirements\/[^/]+\/FR-[^/]+\.md$/.test(rel) && !rel.startsWith('requirements/screens/');

/** 曖昧語の一覧(# 以降は注釈)。 */
export function loadAmbiguityWords(root) {
  return read(root, 'tools/factory/ambiguity-words.txt')
    .split(/\r?\n/)
    .map((l) => l.replace(/#.*/, '').trim())
    .filter(Boolean);
}

/**
 * knowledge/sources.md の表の 1 列目から、出どころの ID を集める。
 * 「SRC-ADR-0002〜0016」のような範囲は、桁数をそろえて展開する。
 */
export function parseSourceIds(text) {
  const ids = new Set();
  for (const line of text.split(/\r?\n/)) {
    const m = /^\|\s*(SRC-[^|\s]+)\s*\|/.exec(line);
    if (!m) continue;
    const range = /^(.*?)(\d+)〜(\d+)$/.exec(m[1]);
    if (!range) {
      ids.add(m[1]);
      continue;
    }
    const [, prefix, from, to] = range;
    for (let n = Number(from); n <= Number(to); n++) ids.add(prefix + String(n).padStart(from.length, '0'));
  }
  return ids;
}

/** 要件のファイルを読み、id → {file, data, sentence, sentenceLine} の Map にする(書式の誤りは ears-lint が指摘する)。 */
export function loadRequirements(root) {
  const map = new Map();
  for (const file of walk(root, 'requirements', isRequirementFile)) {
    const fm = readFrontmatter(read(root, file));
    if (!fm || fm.error || typeof fm.data.id !== 'string') continue;
    const { sentence, line } = requirementSentence(fm);
    map.set(fm.data.id, { file, data: fm.data, sentence, sentenceLine: line });
  }
  return map;
}

/** 「## 要件」の節の本文(空行を除く行)を返す。 */
export function requirementSentence(fm) {
  const lines = fm.body.split(/\r?\n/);
  const start = lines.findIndex((l) => /^##\s+要件\s*$/.test(l));
  if (start < 0) return { sentence: null, lines: [], line: fm.bodyStartLine };
  const body = [];
  let firstLine = null;
  for (let i = start + 1; i < lines.length && !/^##\s/.test(lines[i]); i++) {
    if (lines[i].trim() === '') continue;
    body.push(lines[i].trim());
    firstLine ??= fm.bodyStartLine + i;
  }
  return { sentence: body.length === 1 ? body[0] : null, lines: body, line: firstLine ?? fm.bodyStartLine + start };
}

/** 業務ルールを読み、id → {file, data} の Map にする。 */
export function loadBusinessRules(root) {
  const map = new Map();
  for (const file of walk(root, 'knowledge/business-rules', (f) => f.endsWith('.md'))) {
    const fm = readFrontmatter(read(root, file));
    if (fm && !fm.error && typeof fm.data.id === 'string') map.set(fm.data.id, { file, data: fm.data });
  }
  return map;
}

/**
 * 作業ブランチの名前から工程と Issue 番号を読む(AGENTS.md の命名)。
 * @returns {{role: string, issue: number|null} | null} 命名に合わなければ null
 */
export function parseBranch(branch) {
  if (/^dependabot\//.test(branch)) return { role: 'dependabot', issue: null };
  if (/^factory\/.+/.test(branch)) return { role: 'factory', issue: null };
  const m = /^(knowledge|req|spec|impl|prep)\/(\d+)-.+$/.exec(branch);
  return m ? { role: m[1], issue: Number(m[2]) } : null;
}
