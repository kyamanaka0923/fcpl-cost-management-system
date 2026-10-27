import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkClosingKeywords, findClosingKeywords } from '../checks/closing.mjs';

test('GitHub が閉じると読む書き方を見つける(否定の文の中でも)', () => {
  const body = [
    '- Closes #46 は付けない(Refs #46)',
    'fixes: #12',
    'これは resolved kyamanaka0923/fcpl-cost-management-system#3 です',
    'Close https://github.com/o/r/issues/9',
    'FIXED #7',
  ].join('\n');
  assert.deepEqual(
    findClosingKeywords(body).map((m) => `${m.line}:${m.text}`),
    ['1:Closes #46', '2:fixes: #12', '3:resolved kyamanaka0923/fcpl-cost-management-system#3', '4:Close https://github.com/o/r/issues/9', '5:FIXED #7'],
  );
});

test('閉じない書き方は見つけない', () => {
  const body = ['Refs #46', '#46 を参照', 'closed 状態の Issue', 'fixture #1 を使う', 'closest #2', 'disclose #3', 'Closes なし'].join('\n');
  assert.deepEqual(findClosingKeywords(body), []);
});

test('impl/・factory/・Dependabot の PR は閉じてよい', () => {
  for (const b of ['impl/46-x', 'factory/57-x', 'dependabot/npm/x']) assert.deepEqual(checkClosingKeywords(b, 'Closes #46'), [], b);
});

test('knowledge/・req/・spec/・prep/ の PR で閉じる書き方があれば落とす', () => {
  for (const b of ['knowledge/46-x', 'req/46-x', 'spec/46-x', 'prep/46-x']) {
    const errors = checkClosingKeywords(b, 'Refs #46\n- Closes #46 は付けない');
    assert.equal(errors.length, 1, b);
    assert.match(errors[0].message, /2 行目「Closes #46」.*Refs #番号/);
  }
  assert.deepEqual(checkClosingKeywords('req/46-x', 'Refs #46'), []);
});
