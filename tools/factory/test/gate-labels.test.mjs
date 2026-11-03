import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkIssueLabels } from '../lib/gate-labels.mjs';

const merged = ['knowledge/46-a', 'req/46-b', 'req/46-c', 'spec/46-d', 'req/47-x'];

test('表どおりの組み合わせは指摘 0 件', () => {
  assert.deepEqual(checkIssueLabels(46, ['stage:準備中'], merged), []);
  assert.deepEqual(checkIssueLabels(46, ['stage:準備中', 'gate0-approved'], merged), []);
  assert.deepEqual(checkIssueLabels(46, ['stage:着手待ち', 'gate0-approved', 'gate1-approved'], merged), []);
  assert.deepEqual(checkIssueLabels(46, ['stage:仕様確定', 'gate0-approved', 'gate1-approved', 'gate2-approved'], merged), []);
  assert.deepEqual(checkIssueLabels(46, ['stage:実装中', 'gate0-approved', 'gate1-approved', 'gate2-approved'], merged), []);
  assert.deepEqual(checkIssueLabels(46, ['stage:判断待ち', 'needs-answer', 'gate0-approved'], merged), []);
});

test('列のラベルもゲートラベルもない Issue(工場の改善など)は見ない', () => {
  assert.deepEqual(checkIssueLabels(77, ['factory', 'factory:B'], []), []);
});

test('ゲートを飛ばしたラベルを指摘する', () => {
  const msgs = checkIssueLabels(46, ['stage:仕様確定', 'gate0-approved', 'gate2-approved'], merged);
  assert.ok(msgs.some((m) => /gate2-approved が付いているが、gate1-approved が付いていない/.test(m)), msgs.join('\n'));
  assert.ok(msgs.some((m) => /stage:仕様確定 のときのゲートラベルは/.test(m)), msgs.join('\n'));
});

test('列とゲートラベルが合わないものを指摘する', () => {
  assert.match(checkIssueLabels(46, ['stage:準備中', 'gate0-approved', 'gate1-approved'], merged)[0], /stage:準備中 のときのゲートラベルは ゲートラベルなし か gate0-approved/);
  assert.match(checkIssueLabels(46, ['stage:着手待ち', 'gate0-approved'], merged)[0], /stage:着手待ち のときのゲートラベルは gate0-approved・gate1-approved/);
});

test('列のラベルがない・2 つ以上ある・工場のものでないものを指摘する', () => {
  assert.match(checkIssueLabels(46, ['gate0-approved'], merged)[0], /列のラベル\(stage:…\)が付いていない/);
  assert.match(checkIssueLabels(46, ['stage:準備中', 'stage:着手待ち'], merged)[0], /列のラベルが 2 つ付いている/);
  assert.match(checkIssueLabels(46, ['stage:完了'], merged)[0], /工場の列のラベルではない/);
});

test('ゲートラベルの工程の PR がマージされていなければ指摘する(ゲート0 は PR を求めない)', () => {
  const msgs = checkIssueLabels(48, ['stage:仕様確定', 'gate0-approved', 'gate1-approved', 'gate2-approved'], merged);
  assert.deepEqual(msgs, [
    'gate1-approved が付いているが、req/48-… の PR が main にマージされていない(ゲートは、その工程の PR を人がマージして通す)',
    'gate2-approved が付いているが、spec/48-… の PR が main にマージされていない(ゲートは、その工程の PR を人がマージして通す)',
  ]);
  assert.deepEqual(checkIssueLabels(48, ['stage:準備中', 'gate0-approved'], []), []);
});

test('ラベルから進めてよい工程を選ぶ', async () => {
  const { selectStage } = await import('../lib/gate-labels.mjs');
  const all = ['gate0-approved', 'gate1-approved', 'gate2-approved'];
  assert.deepEqual(selectStage(['stage:準備中']), { role: 'knowledge' });
  assert.deepEqual(selectStage(['stage:準備中', 'gate0-approved']), { role: 'req' });
  assert.deepEqual(selectStage(['stage:着手待ち', 'gate0-approved', 'gate1-approved']), { role: 'spec' });
  assert.deepEqual(selectStage(['stage:仕様確定', ...all]), { role: 'impl' });
  assert.deepEqual(selectStage(['stage:実装中', ...all, 'enhancement']), { role: 'impl' });
});

test('進めてよい工程がないときは理由を返す', async () => {
  const { selectStage } = await import('../lib/gate-labels.mjs');
  assert.match(selectStage(['stage:判断待ち', 'gate0-approved']).reason, /回答待ち/);
  assert.match(selectStage(['stage:着手待ち', 'gate0-approved', 'gate1-approved', 'needs-answer']).reason, /回答待ち/);
  assert.match(selectStage(['factory', 'factory:C']).reason, /工場の改善/);
  assert.match(selectStage(['gate0-approved']).reason, /ちょうど1つでない/);
  assert.match(selectStage(['stage:準備中', 'stage:着手待ち']).reason, /ちょうど1つでない/);
  assert.match(selectStage(['stage:着手待ち', 'gate0-approved']).reason, /表にない/);
  assert.match(selectStage(['stage:仕様確定', 'gate0-approved', 'gate2-approved']).reason, /表にない/);
});

const all3 = ['gate0-approved', 'gate1-approved', 'gate2-approved'];

test('redo:<工程> があれば、ゲートラベルを外さずにその工程を選ぶ(上流のやり直し。#102)', async () => {
  const { selectStage } = await import('../lib/gate-labels.mjs');
  assert.deepEqual(selectStage(['stage:実装中', ...all3, 'redo:req']), { role: 'req', redo: true });
  assert.deepEqual(selectStage(['stage:実装中', ...all3, 'redo:spec']), { role: 'spec', redo: true });
  assert.deepEqual(selectStage(['stage:準備中', 'gate0-approved', 'redo:knowledge']), { role: 'knowledge', redo: true });
});

test('redo:<工程> が使えないときは理由を返す', async () => {
  const { selectStage } = await import('../lib/gate-labels.mjs');
  assert.match(selectStage(['stage:実装中', ...all3, 'redo:req', 'redo:spec']).reason, /2 つある/);
  assert.match(selectStage(['stage:実装中', ...all3, 'redo:impl']).reason, /やり直せる工程ではない/);
  assert.match(selectStage(['stage:準備中', 'redo:req']).reason, /gate0-approved が要る/);
  assert.match(selectStage(['stage:判断待ち', ...all3, 'needs-answer', 'redo:req']).reason, /回答待ち/, '回答待ちの間は redo でも進めない');
});

test('ラベルの検査: redo:<工程> は 1 つだけ、やり直せる工程だけ', () => {
  assert.deepEqual(checkIssueLabels(46, ['stage:実装中', ...all3, 'redo:req'], merged), []);
  assert.match(checkIssueLabels(46, ['stage:実装中', ...all3, 'redo:req', 'redo:spec'], merged)[0], /redo:…\)が 2 つ付いている/);
  assert.match(checkIssueLabels(46, ['stage:実装中', ...all3, 'redo:impl'], merged)[0], /redo:impl はやり直せる工程ではない/);
});

test('回答のあと(needs-answer を外した)は、問いのコメントの印の列に戻す(#102)', async () => {
  const { labelsAfterAnswer } = await import('../lib/gate-labels.mjs');
  const q = (stage) => `## [D] 実装を止めました\n<!-- factory:return-stage=${stage} -->\n問い…`;
  const labels = ['enhancement', 'stage:判断待ち', ...all3];
  assert.deepEqual(labelsAfterAnswer(labels, [q('stage:着手待ち'), '回答', q('stage:実装中'), '## 回答\n1. C']), {
    labels: ['enhancement', ...all3, 'stage:実装中'],
    restore: 'stage:実装中',
  });
});

test('回答のあとの戻し: 回答待ち・判断待ちでないときは変えず、印がない・戻せない列なら止める', async () => {
  const { labelsAfterAnswer } = await import('../lib/gate-labels.mjs');
  const waiting = ['stage:判断待ち', 'needs-answer', ...all3];
  assert.deepEqual(labelsAfterAnswer(waiting, []), { labels: waiting, restore: null });
  assert.deepEqual(labelsAfterAnswer(['stage:実装中', ...all3], []), { labels: ['stage:実装中', ...all3], restore: null });
  assert.match(labelsAfterAnswer(['stage:判断待ち', ...all3], ['回答']).error, /印がない/);
  assert.match(labelsAfterAnswer(['stage:判断待ち', ...all3], ['<!-- factory:return-stage=stage:完了 -->']).error, /戻せる列ではない/);
});
