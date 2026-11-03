import { test } from 'node:test';
import assert from 'node:assert/strict';
import { apiSpecFile, applyApiSpec, checkApiSpecImplemented, checkApiSpecShape, findMissing } from '../checks/api-spec.mjs';

const FILE = apiSpecFile(45);

const openapi = {
  openapi: '3.1.1',
  paths: {
    '/api/budgets/{budgetId}': { get: { responses: { 200: { content: { 'application/json': { schema: { $ref: '#/components/schemas/BudgetDetailDto' } } } } } } },
    '/api/old': { delete: { responses: { 204: {} } } },
  },
  components: {
    schemas: {
      BudgetDetailDto: { type: 'object', required: ['id'], properties: { id: { type: 'string' } } },
      OldDto: { type: 'object' },
    },
  },
};

const spec = {
  paths: {
    '/api/budgets/{budgetId}/lines': {
      put: {
        parameters: [{ name: 'updateNumber', in: 'query', required: true, schema: { type: 'integer' } }],
        responses: { 200: { content: { 'application/json': { schema: { $ref: '#/components/schemas/BudgetDetailDto' } } } }, 409: {} },
      },
    },
    '/api/old': { delete: null },
  },
  components: {
    schemas: {
      BudgetDetailDto: { type: 'object', required: ['id', 'updateNumber'], properties: { id: { type: 'string' }, updateNumber: { type: 'integer' } } },
      OldDto: null,
    },
  },
};

test('仕様の API のファイルは Issue ごとに specs/api/ に置く', () => {
  assert.equal(FILE, 'specs/api/ISSUE-45.openapi.json');
});

test('仕様の差分を当てると、書いた操作とスキーマが置き換わり、null は消える', () => {
  const applied = applyApiSpec(openapi, spec);
  assert.ok(applied.paths['/api/budgets/{budgetId}/lines'].put);
  assert.ok(applied.paths['/api/budgets/{budgetId}'].get, '書いていない操作は残る');
  assert.equal(applied.paths['/api/old'], undefined, '操作がなくなったパスは消える');
  assert.deepEqual(applied.components.schemas.BudgetDetailDto.required, ['id', 'updateNumber']);
  assert.equal(applied.components.schemas.OldDto, undefined);
  assert.ok(openapi.paths['/api/old'], '元の OpenAPI は変えない');
});

test('[C] 形の正しい仕様の差分は指摘なし', () => {
  assert.deepEqual(checkApiSpecShape(FILE, spec, openapi), []);
});

test('[C] 書けない場所・メソッドでない名前・消す対象がないものを指摘する', () => {
  const msgs = checkApiSpecShape(
    FILE,
    {
      info: {},
      components: { responses: {}, schemas: { NoSuch: null } },
      paths: { 'api/x': { get: {} }, '/api/y': { fetch: {}, post: null, put: 'x' } },
    },
    openapi,
  ).map((e) => e.message);
  assert.deepEqual(msgs, [
    '「info」は書けない。書けるのは paths と components.schemas だけ',
    'components の「responses」は書けない。書けるのは components.schemas だけ',
    'パス「api/x」は / で始める',
    '/api/y: 「fetch」は HTTP のメソッドではない(get・put・post・delete・patch・head・options・trace)',
    'POST /api/y: 消す操作が docs/api/openapi.json にない',
    'PUT /api/y: 操作は丸ごと書く(オブジェクト)か、消すなら null にする',
    'スキーマ NoSuch: 消すスキーマが docs/api/openapi.json にない',
  ]);
});

test('[C] 差分を当てたあとに解決できない $ref を指摘する', () => {
  const msgs = checkApiSpecShape(FILE, { components: { schemas: { BudgetDetailDto: null } } }, openapi).map((e) => e.message);
  assert.deepEqual(msgs, ['$ref「#/components/schemas/BudgetDetailDto」のスキーマが、差分を当てた OpenAPI にない']);
});

test('[C] JSON のオブジェクトでなければ指摘する', () => {
  assert.match(checkApiSpecShape(FILE, [], openapi)[0].message, /JSON のオブジェクトにする/);
});

test('食い違いの判定: 実装に多い説明・format は許し、仕様に書いたことが欠けていれば場所を返す', () => {
  assert.equal(findMissing({ type: 'integer' }, { type: 'integer', format: 'int32', description: '更新番号' }), null);
  assert.equal(findMissing({ required: ['b', 'a'] }, { required: ['a', 'b', 'c'] }), null, '配列の順番は問わない');
  assert.equal(findMissing({ required: ['a', 'x'] }, { required: ['a'] }), '/required/1: "x" に当たる要素がない');
  assert.equal(findMissing({ properties: { n: { type: 'integer' } } }, { properties: { n: { type: 'string' } } }), '/properties/n/type: 仕様は "integer"、実装は "string"');
  assert.equal(findMissing({ responses: { 409: {} } }, { responses: { 200: {} } }), '/responses/409 がない');
});

test('[D] 実装から作り直した OpenAPI に仕様がすべて入っていれば指摘なし', () => {
  const implemented = applyApiSpec(openapi, spec);
  implemented.components.schemas.BudgetDetailDto.properties.updateNumber.format = 'int32';
  assert.deepEqual(checkApiSpecImplemented(FILE, spec, implemented), []);
});

test('[D] 実装がない・食い違う・消したはずのものが残る、を指摘する', () => {
  const msgs = checkApiSpecImplemented(FILE, spec, openapi).map((e) => e.message);
  assert.deepEqual(msgs, [
    'PUT /api/budgets/{budgetId}/lines: docs/api/openapi.json にない',
    'DELETE /api/old: 仕様では消したが、docs/api/openapi.json にまだある',
    'スキーマ BudgetDetailDto: 仕様と食い違う(/required/1: "updateNumber" に当たる要素がない)',
    'スキーマ OldDto: 仕様では消したが、docs/api/openapi.json にまだある',
  ]);
});
