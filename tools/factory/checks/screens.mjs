// fixtures-schema: 画面要件(screens.yaml)の形と、fixtures が OpenAPI の応答のスキーマに合うかを検査する(requirements/screens/README.md)。
import Ajv2020 from 'ajv/dist/2020.js';
import { parse as parseYaml } from 'yaml';
import { issue } from '../lib/common.mjs';

const SCREENS = 'requirements/screens/screens.yaml';
const fixturePath = (id, state) => `requirements/screens/fixtures/${id}/${state}.json`;

/** "GET /api/x/{id}" を OpenAPI のパスとメソッドに分ける。 */
export function parseRequestKey(key) {
  const m = /^(GET|POST|PUT|PATCH|DELETE) (\/\S*)$/.exec(key);
  return m ? { method: m[1].toLowerCase(), path: m[2] } : null;
}

/** OpenAPI の応答 1 つのスキーマで、値を検査する関数を作る(components への $ref を解決できるよう、components を同じ文書に入れる)。 */
function validatorFor(ajv, openapi, schema) {
  return ajv.compile({ ...schema, components: openapi.components });
}

/**
 * @param {string|null} screensText screens.yaml の中身
 * @param {(rel: string) => string|null} readFile リポジトリのファイルを読む(なければ null)
 * @param {string[]} fixtureFiles requirements/screens/fixtures/ 以下のファイル
 * @param {object} openapi docs/api/openapi.json
 * @param {(id: string) => boolean} requirementExists 要件 ID があるか
 */
export function checkScreens(screensText, readFile, fixtureFiles, openapi, requirementExists) {
  if (screensText === null) return [];
  let doc;
  try {
    doc = parseYaml(screensText);
  } catch (e) {
    return [issue(SCREENS, 1, `YAML が読めない: ${e.message}`)];
  }
  if (!Array.isArray(doc?.screens)) return [issue(SCREENS, 1, 'screens は配列で書く')];

  const out = [];
  const err = (msg) => out.push(issue(SCREENS, 0, msg));
  const ajv = new Ajv2020({ strict: false, validateFormats: false, allErrors: true });
  const ids = new Set();
  const expectedFixtures = new Set();

  const checkRequest = (where, key) => {
    const r = parseRequestKey(key);
    if (!r) return err(`${where}: 「${key}」は「<メソッド> <パス>」の形で書く`), null;
    const op = openapi.paths?.[r.path]?.[r.method];
    if (!op) return err(`${where}: ${key} が docs/api/openapi.json にない`), null;
    return op;
  };

  for (const s of doc.screens) {
    const at = s?.id ?? '(id なし)';
    if (!/^SCR-\d{3}$/.test(s?.id ?? '')) err(`${at}: id は SCR-<3 桁>`);
    else if (ids.has(s.id)) err(`${at}: id が重複している`);
    ids.add(s?.id);
    for (const key of ['title', 'route', 'page']) if (typeof s?.[key] !== 'string' || !s[key]) err(`${at}: ${key} がない`);
    if (typeof s?.route === 'string' && !s.route.startsWith('/')) err(`${at}: route は / で始める`);
    if (typeof s?.page === 'string' && readFile(s.page) === null) err(`${at}: page ${s.page} がない`);
    if (!Array.isArray(s?.requirements) || s.requirements.length === 0) err(`${at}: requirements(この画面が満たす要件)がない`);
    else for (const r of s.requirements) if (!requirementExists(r)) err(`${at}: 要件 ${r} が requirements/ にも docs/REQUIREMENTS.md にもない`);
    if (!Array.isArray(s?.items) || s.items.length === 0) err(`${at}: items(画面に出るもの)がない`);
    for (const op of s?.operations ?? []) checkRequest(`${at} の操作「${op?.name}」`, op?.request ?? '');
    if (!Array.isArray(s?.states) || s.states.length === 0) {
      err(`${at}: states がない(少なくとも「通常」を置く)`);
      continue;
    }
    const names = new Set();
    for (const st of s.states) {
      if (typeof st?.name !== 'string' || !st.name) {
        err(`${at}: 状態に name がない`);
        continue;
      }
      if (names.has(st.name)) err(`${at}: 状態「${st.name}」が重複している`);
      names.add(st.name);
      if (typeof st.shows !== 'string' || !st.shows) err(`${at} の状態「${st.name}」: shows(何が見えるか)がない`);
      const file = fixturePath(s.id, st.name);
      expectedFixtures.add(file);
      const text = readFile(file);
      if (text === null) {
        err(`${at} の状態「${st.name}」: fixtures ${file} がない`);
        continue;
      }
      out.push(...checkFixture(file, text, openapi, ajv, checkRequest));
    }
  }

  for (const f of fixtureFiles) if (!expectedFixtures.has(f)) out.push(issue(f, 0, 'screens.yaml のどの状態からも使われていない fixtures'));
  return out;
}

function checkFixture(file, text, openapi, ajv, checkRequest) {
  let data;
  try {
    data = JSON.parse(text);
  } catch (e) {
    return [issue(file, 1, `JSON が読めない: ${e.message}`)];
  }
  if (typeof data !== 'object' || data === null || Array.isArray(data)) return [issue(file, 1, '「<メソッド> <パス>」をキーにしたオブジェクトで書く')];
  const out = [];
  for (const [key, res] of Object.entries(data)) {
    const op = checkRequest(file, key);
    if (!op) continue;
    const response = op.responses?.[String(res?.status)];
    if (!response) {
      out.push(issue(file, 0, `${key} の status ${res?.status} は OpenAPI の応答にない(${Object.keys(op.responses ?? {}).join('・')})`));
      continue;
    }
    const schema = response.content?.['application/json']?.schema;
    if (!schema) {
      if (res.body !== undefined) out.push(issue(file, 0, `${key} の ${res.status} は本文を返さない(body を消す)`));
      continue;
    }
    const validate = validatorFor(ajv, openapi, schema);
    if (!validate(res.body)) {
      const why = validate.errors.map((e) => `${e.instancePath || '(本文)'} ${e.message}`).join('、');
      out.push(issue(file, 0, `${key} の body が OpenAPI のスキーマに合わない: ${why}`));
    }
  }
  return out;
}
