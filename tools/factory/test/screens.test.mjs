import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkScreens, parseRequestKey } from '../checks/screens.mjs';

const openapi = {
  paths: {
    '/api/cost-elements': {
      get: {
        responses: {
          200: { content: { 'application/json': { schema: { type: 'array', items: { $ref: '#/components/schemas/CostElementDto' } } } } },
          400: { content: { 'application/json': { schema: { $ref: '#/components/schemas/ErrorResponse' } } } },
        },
      },
      post: { responses: { 201: { content: { 'application/json': { schema: { $ref: '#/components/schemas/CostElementDto' } } } } } },
    },
    '/api/projects/{id}': { delete: { responses: { 204: { description: 'No Content' } } } },
  },
  components: {
    schemas: {
      CostElementDto: { type: 'object', required: ['code', 'name'], properties: { code: { type: 'string' }, name: { type: 'string' } } },
      ErrorResponse: { type: 'object', required: ['error'], properties: { error: { type: 'string' } } },
    },
  },
};

const yaml = (extra = '') => `screens:
  - id: SCR-001
    title: 費目マスタ
    route: /cost-elements
    page: frontend/src/pages/CostElementsPage.tsx
    requirements: [FR-3.1]
    items: [費目の表]
    operations:
      - name: 費目を追加する
        request: POST /api/cost-elements
    states:
      - name: 通常
        shows: 2 行が出る
${extra}`;

const files = (fixtures) => ({
  'frontend/src/pages/CostElementsPage.tsx': 'x',
  ...fixtures,
});
const run = (text, fixtures, exists = () => true) => {
  const all = files(fixtures);
  const fixtureFiles = Object.keys(fixtures).filter((f) => f.startsWith('requirements/screens/fixtures/'));
  return checkScreens(text, (p) => all[p] ?? null, fixtureFiles, openapi, exists).map((e) => `${e.file}: ${e.message}`);
};
const ok = { 'requirements/screens/fixtures/SCR-001/通常.json': JSON.stringify({ 'GET /api/cost-elements': { status: 200, body: [{ code: 'A', name: 'a' }] } }) };

test('リクエストのキーをメソッドとパスに分ける', () => {
  assert.deepEqual(parseRequestKey('GET /api/projects/{id}'), { method: 'get', path: '/api/projects/{id}' });
  assert.equal(parseRequestKey('get /api/x'), null);
});

test('形のそろった画面要件と、スキーマに合う fixtures は通る', () => {
  assert.deepEqual(run(yaml(), ok), []);
});

test('エラーの応答と、本文のない応答の fixtures も通る', () => {
  const fixtures = {
    'requirements/screens/fixtures/SCR-001/通常.json': JSON.stringify({
      'GET /api/cost-elements': { status: 400, body: { error: '読めない' } },
      'DELETE /api/projects/{id}': { status: 204 },
    }),
  };
  assert.deepEqual(run(yaml(), fixtures), []);
});

test('スキーマに合わない body・ない status・ない API を指摘する', () => {
  const fixtures = {
    'requirements/screens/fixtures/SCR-001/通常.json': JSON.stringify({
      'GET /api/cost-elements': { status: 200, body: [{ code: 1 }] },
      'POST /api/cost-elements': { status: 500, body: {} },
      'GET /api/nope': { status: 200, body: [] },
      'DELETE /api/projects/{id}': { status: 204, body: {} },
    }),
  };
  const msgs = run(yaml(), fixtures).join('\n');
  assert.match(msgs, /body が OpenAPI のスキーマに合わない: \/0 must have required property 'name'/);
  assert.match(msgs, /status 500 は OpenAPI の応答にない\(201\)/);
  assert.match(msgs, /GET \/api\/nope が docs\/api\/openapi.json にない/);
  assert.match(msgs, /本文を返さない/);
});

test('fixtures がない状態・使われていない fixtures・ない要件・ない画面ファイルを指摘する', () => {
  const extra = { ...ok, 'requirements/screens/fixtures/SCR-001/古い.json': '{}' };
  const msgs = run(yaml('      - name: 空\n        shows: 空と出る\n'), extra, (id) => id !== 'FR-3.1').join('\n');
  assert.match(msgs, /状態「空」: fixtures requirements\/screens\/fixtures\/SCR-001\/空.json がない/);
  assert.match(msgs, /古い.json: screens.yaml のどの状態からも使われていない/);
  assert.match(msgs, /要件 FR-3.1 が requirements\/ にも docs\/REQUIREMENTS.md にもない/);
  const noPage = checkScreens(yaml(), (p) => ok[p] ?? null, Object.keys(ok), openapi, () => true).map((e) => e.message).join('\n');
  assert.match(noPage, /page frontend\/src\/pages\/CostElementsPage.tsx がない/);
});

test('ID の形・重複・状態の欠け・ない操作の API を指摘する', () => {
  const text = `screens:
  - id: SCR-1
    title: a
    route: a
    page: frontend/src/pages/CostElementsPage.tsx
    requirements: [FR-3.1]
    items: [x]
    operations:
      - name: 消す
        request: DELETE /api/cost-elements
    states: []
  - id: SCR-002
    title: b
    route: /b
    page: frontend/src/pages/CostElementsPage.tsx
    requirements: [FR-3.1]
    items: [x]
    states:
      - name: 通常
      - name: 通常
`;
  const msgs = run(text, {
    'requirements/screens/fixtures/SCR-002/通常.json': JSON.stringify({ 'GET /api/cost-elements': { status: 200, body: [] } }),
  }).join('\n');
  assert.match(msgs, /SCR-1: id は SCR-<3 桁>/);
  assert.match(msgs, /route は \/ で始める/);
  assert.match(msgs, /DELETE \/api\/cost-elements が docs\/api\/openapi.json にない/);
  assert.match(msgs, /states がない/);
  assert.match(msgs, /状態「通常」が重複している/);
  assert.match(msgs, /shows\(何が見えるか\)がない/);
});

test('screens.yaml がなければ何もしない', () => {
  assert.deepEqual(checkScreens(null, () => null, [], openapi, () => true), []);
});
