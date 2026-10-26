#!/usr/bin/env node
// 工場のゲートの検査(ADR-0015 フェーズ4)。リポジトリ直下で実行する。
//   node tools/factory/cli.mjs <検査名>
// 検査名: ears | ambiguity | coverage | knowledge | role | gate | premises | factory-version
// PR の検査(role・gate・premises・factory-version)は、次の環境変数を使う(.github/workflows/factory.yml が渡す)。
//   FACTORY_BRANCH  PR のブランチ名。空なら PR ではない(main への push)ので何もしない
//   FACTORY_BASE    PR の base のコミット(main)
//   FACTORY_HEAD    PR の head のコミット
//   GITHUB_REPOSITORY / GH_TOKEN  gate で Issue のラベルを読む
import { execFileSync } from 'node:child_process';
import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  isRequirementFile,
  loadAmbiguityWords,
  loadBusinessRules,
  loadRequirements,
  parseSourceIds,
  read,
  walk,
} from './lib/common.mjs';
import { parseFeature } from './lib/gherkin.mjs';
import { checkRequirementFile } from './checks/ears.mjs';
import { findAmbiguousWords, isAmbiguityTarget } from './checks/ambiguity.mjs';
import { audit, matrixMarkdown } from './checks/coverage.mjs';
import { checkBusinessRule, checkRequirementRules } from './checks/knowledge.mjs';
import { checkFactoryVersion, checkGate, checkRole } from './checks/branch.mjs';
import { checkPremises } from './checks/premises.mjs';

const root = process.cwd();
const env = process.env;
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim();

function changedFiles() {
  const out = git('diff', '--name-only', '--no-renames', `${env.FACTORY_BASE}...${env.FACTORY_HEAD}`);
  return out ? out.split('\n') : [];
}

function blobOnMain(path) {
  try {
    return git('rev-parse', '-q', '--verify', `${env.FACTORY_BASE}:${path}`);
  } catch {
    return null;
  }
}

function issueLabels(n) {
  try {
    const out = execFileSync('gh', ['issue', 'view', String(n), '--repo', env.GITHUB_REPOSITORY, '--json', 'labels', '--jq', '[.labels[].name]'], {
      encoding: 'utf8',
    });
    return JSON.parse(out);
  } catch {
    return null;
  }
}

const featureFiles = () => walk(root, 'tests', (f) => f.endsWith('.feature')).map((file) => ({ file, parsed: parseFeature(read(root, file)) }));

/** 検査ごとの中身。指摘の配列と、実行サマリに足す Markdown を返す。 */
const CHECKS = {
  ears: () => ({ errors: walk(root, 'requirements', isRequirementFile).flatMap((f) => checkRequirementFile(f, read(root, f))) }),

  ambiguity: () => {
    const words = loadAmbiguityWords(root);
    const files = [...walk(root, 'requirements', isAmbiguityTarget), ...walk(root, 'knowledge', isAmbiguityTarget), ...walk(root, 'tests', isAmbiguityTarget)];
    return { errors: files.flatMap((f) => findAmbiguousWords(f, read(root, f), words)) };
  },

  coverage: () => {
    const { errors, matrix } = audit(loadRequirements(root), new Set(loadBusinessRules(root).keys()), featureFiles());
    const md = matrixMarkdown(matrix);
    writeFileSync(join(root, 'coverage-matrix.md'), `# 要件とシナリオのトレーサビリティ\n\n${md}`);
    return { errors, summary: `### 要件とシナリオのトレーサビリティ\n\n${md}` };
  },

  knowledge: () => {
    const sourceIds = parseSourceIds(read(root, 'knowledge/sources.md'));
    const rules = walk(root, 'knowledge/business-rules', (f) => f.endsWith('.md'));
    return {
      errors: [
        ...rules.flatMap((f) => checkBusinessRule(f, read(root, f), sourceIds)),
        ...checkRequirementRules(loadRequirements(root), loadBusinessRules(root)),
      ],
    };
  },

  role: () => ({ errors: checkRole(env.FACTORY_BRANCH, changedFiles()), pr: true }),

  gate: () => {
    const n = /^[a-z]+\/(\d+)-/.exec(env.FACTORY_BRANCH)?.[1];
    return { errors: checkGate(env.FACTORY_BRANCH, n ? issueLabels(n) : []), pr: true };
  },

  premises: () => {
    const n = /^[a-z]+\/(\d+)-/.exec(env.FACTORY_BRANCH)?.[1];
    const file = `.factory/premises/ISSUE-${n}.yml`;
    const text = n && existsSync(join(root, file)) ? read(root, file) : null;
    return { errors: checkPremises(env.FACTORY_BRANCH, text, blobOnMain), pr: true };
  },

  'factory-version': () => ({ errors: checkFactoryVersion(env.FACTORY_BRANCH, changedFiles()), pr: true }),
};

const PR_ONLY = new Set(['role', 'gate', 'premises', 'factory-version']);

function main() {
  const name = process.argv[2];
  const check = CHECKS[name];
  if (!check) {
    console.error(`検査名を指定してください: ${Object.keys(CHECKS).join(' | ')}`);
    process.exit(2);
  }
  if (PR_ONLY.has(name) && !env.FACTORY_BRANCH) {
    console.log(`${name}: PR ではないので検査しません(FACTORY_BRANCH が空)`);
    return;
  }
  const { errors, summary } = check();
  for (const e of errors) {
    const where = e.file ? `${e.file}${e.line ? `:${e.line}` : ''}: ` : '';
    if (env.GITHUB_ACTIONS) {
      const props = [e.file && `file=${e.file}`, e.line && `line=${e.line}`].filter(Boolean).join(',');
      console.log(`::error ${props}::${e.message}`);
    }
    console.log(`${where}${e.message}`);
  }
  console.log(`${name}: 指摘 ${errors.length} 件`);
  if (env.GITHUB_STEP_SUMMARY) {
    const list = errors.map((e) => `- ${e.file ? `\`${e.file}${e.line ? `:${e.line}` : ''}\` ` : ''}${e.message}`).join('\n');
    appendFileSync(env.GITHUB_STEP_SUMMARY, `## ${name}: 指摘 ${errors.length} 件\n\n${list}\n\n${summary ?? ''}\n`);
  }
  if (errors.length > 0) process.exit(1);
}

main();
