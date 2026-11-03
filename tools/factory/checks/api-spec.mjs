// 仕様の API(specs/api/ISSUE-<n>.openapi.json)の検査(#98)。
// [C] は docs/api/openapi.json を変えず、変える操作とスキーマだけを、OpenAPI と同じ形の部分の文書に書く。
//   { "paths": { "<パス>": { "<メソッド>": 操作 | null } }, "components": { "schemas": { "<名前>": スキーマ | null } } }
// 書いた操作・スキーマは丸ごと置き換わる(なければ足す)。null は消す。
// [D] は実装から docs/api/openapi.json を作り直し、仕様に書いたことがすべて入っているかを確かめる。
import { issue } from '../lib/common.mjs';

/** 仕様の API のファイル。 */
export const apiSpecFile = (n) => `specs/api/ISSUE-${n}.openapi.json`;

const METHODS = ['get', 'put', 'post', 'delete', 'patch', 'head', 'options', 'trace'];
const isObject = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);
const opName = (path, method) => `${method.toUpperCase()} ${path}`;

function* operations(spec) {
  for (const [path, item] of Object.entries(spec.paths ?? {})) {
    for (const [method, op] of Object.entries(item ?? {})) yield { path, method, op };
  }
}

const schemas = (spec) => Object.entries(spec.components?.schemas ?? {});

/** 仕様の差分を docs/api/openapi.json に当てた文書を返す(元は変えない)。 */
export function applyApiSpec(openapi, spec) {
  const out = structuredClone(openapi);
  out.paths ??= {};
  for (const { path, method, op } of operations(spec)) {
    if (op === null) {
      if (out.paths[path]) delete out.paths[path][method];
      if (out.paths[path] && Object.keys(out.paths[path]).length === 0) delete out.paths[path];
    } else {
      out.paths[path] = { ...out.paths[path], [method]: op };
    }
  }
  for (const [name, schema] of schemas(spec)) {
    out.components ??= {};
    out.components.schemas ??= {};
    if (schema === null) delete out.components.schemas[name];
    else out.components.schemas[name] = schema;
  }
  return out;
}

function refs(value, found = new Set()) {
  if (Array.isArray(value)) value.forEach((v) => refs(v, found));
  else if (isObject(value)) {
    for (const [k, v] of Object.entries(value)) {
      if (k === '$ref' && typeof v === 'string') found.add(v);
      else refs(v, found);
    }
  }
  return found;
}

/**
 * [C]: 仕様の差分の形を確かめる。当てた結果の $ref がすべて解決できるかも見る。
 * @param {string} file 仕様の API のファイル
 * @param {unknown} spec 読んだ JSON
 * @param {object} openapi いまの docs/api/openapi.json(main の版)
 */
export function checkApiSpecShape(file, spec, openapi) {
  if (!isObject(spec)) return [issue(file, 0, 'JSON のオブジェクトにする({ "paths": …, "components": { "schemas": … } })')];
  const errors = [];
  const err = (m) => errors.push(issue(file, 0, m));
  for (const key of Object.keys(spec)) {
    if (!['paths', 'components'].includes(key)) err(`「${key}」は書けない。書けるのは paths と components.schemas だけ`);
  }
  for (const key of Object.keys(spec.components ?? {})) {
    if (key !== 'schemas') err(`components の「${key}」は書けない。書けるのは components.schemas だけ`);
  }
  for (const [path, item] of Object.entries(spec.paths ?? {})) {
    if (!path.startsWith('/')) err(`パス「${path}」は / で始める`);
    if (!isObject(item)) {
      err(`${path}: メソッドごとの操作を書く({ "put": … })`);
      continue;
    }
    for (const [method, op] of Object.entries(item)) {
      if (!METHODS.includes(method)) err(`${path}: 「${method}」は HTTP のメソッドではない(${METHODS.join('・')})`);
      else if (op === null && !openapi.paths?.[path]?.[method]) err(`${opName(path, method)}: 消す操作が docs/api/openapi.json にない`);
      else if (op !== null && !isObject(op)) err(`${opName(path, method)}: 操作は丸ごと書く(オブジェクト)か、消すなら null にする`);
    }
  }
  for (const [name, schema] of schemas(spec)) {
    if (schema === null && !openapi.components?.schemas?.[name]) err(`スキーマ ${name}: 消すスキーマが docs/api/openapi.json にない`);
    else if (schema !== null && !isObject(schema)) err(`スキーマ ${name}: スキーマは丸ごと書く(オブジェクト)か、消すなら null にする`);
  }
  if (errors.length > 0) return errors;
  const applied = applyApiSpec(openapi, spec);
  for (const ref of refs(applied)) {
    const m = /^#\/components\/schemas\/(.+)$/.exec(ref);
    if (m && !applied.components?.schemas?.[m[1]]) err(`$ref「${ref}」のスキーマが、差分を当てた OpenAPI にない`);
  }
  return errors;
}

/**
 * expected に書いたことが actual にすべてあるか。なければ食い違いの場所と中身を返す。
 * 実装から作った OpenAPI には、仕様に書かない説明・format などが足されることがあるので、多い分は許す。
 * 配列は、expected の要素のどれもが actual のどれかの要素に含まれていればよい(順番は問わない)。
 */
export function findMissing(expected, actual, at = '') {
  if (Array.isArray(expected)) {
    if (!Array.isArray(actual)) return `${at || '/'}: 配列のはずが ${JSON.stringify(actual)}`;
    for (const [i, e] of expected.entries()) {
      if (!actual.some((a) => findMissing(e, a) === null)) return `${at}/${i}: ${JSON.stringify(e)} に当たる要素がない`;
    }
    return null;
  }
  if (isObject(expected)) {
    if (!isObject(actual)) return `${at || '/'}: オブジェクトのはずが ${JSON.stringify(actual)}`;
    for (const [k, v] of Object.entries(expected)) {
      if (!(k in actual)) return `${at}/${k} がない`;
      const miss = findMissing(v, actual[k], `${at}/${k}`);
      if (miss) return miss;
    }
    return null;
  }
  return Object.is(expected, actual) ? null : `${at || '/'}: 仕様は ${JSON.stringify(expected)}、実装は ${JSON.stringify(actual)}`;
}

/**
 * [D]: 実装から作り直した docs/api/openapi.json に、仕様の差分がすべて入っているか。
 * @param {string} file 仕様の API のファイル
 * @param {object} spec 仕様の差分
 * @param {object} openapi 実装から作り直した docs/api/openapi.json
 */
export function checkApiSpecImplemented(file, spec, openapi) {
  const errors = [];
  const err = (m) => errors.push(issue(file, 0, m));
  for (const { path, method, op } of operations(spec)) {
    const actual = openapi.paths?.[path]?.[method];
    if (op === null) {
      if (actual) err(`${opName(path, method)}: 仕様では消したが、docs/api/openapi.json にまだある`);
    } else if (!actual) {
      err(`${opName(path, method)}: docs/api/openapi.json にない`);
    } else {
      const miss = findMissing(op, actual);
      if (miss) err(`${opName(path, method)}: 仕様と食い違う(${miss})`);
    }
  }
  for (const [name, schema] of schemas(spec)) {
    const actual = openapi.components?.schemas?.[name];
    if (schema === null) {
      if (actual) err(`スキーマ ${name}: 仕様では消したが、docs/api/openapi.json にまだある`);
    } else if (!actual) {
      err(`スキーマ ${name}: docs/api/openapi.json にない`);
    } else {
      const miss = findMissing(schema, actual);
      if (miss) err(`スキーマ ${name}: 仕様と食い違う(${miss})`);
    }
  }
  return errors;
}
