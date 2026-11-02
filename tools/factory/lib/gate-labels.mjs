// 作業項目(Issue)のゲートラベル・列のラベルと、マージ済みの工程の PR の組み合わせを確かめる(#77)。
// 人がゲートラベルを付け間違えて工程を飛ばしても、CI とラベルの付け外しのたびの検査で止める。
// 正しい組み合わせと、そこから選ぶ工程の表の正本はこのファイル(selectStage)。
// 工場のラインの知識 .takt/facets/knowledge/factory-line.md はこれを説明する。

export const GATES = ['gate0-approved', 'gate1-approved', 'gate2-approved'];

/** 工程が着手するのに要るゲートラベル(その工程までのすべて)。 */
export const REQUIRED_GATES = {
  req: ['gate0-approved'],
  spec: ['gate0-approved', 'gate1-approved'],
  impl: ['gate0-approved', 'gate1-approved', 'gate2-approved'],
};

/**
 * ゲートラベルの裏付けになる、マージ済みの工程の PR のブランチの種類。
 * ゲート0 は、ナレッジに足すものがない作業項目では PR なしで付く(ラインの知識 .takt/facets/knowledge/factory-line.md)ので求めない。
 */
export const GATE_EVIDENCE = { 'gate1-approved': 'req', 'gate2-approved': 'spec' };

/** 列のラベルごとに、そろっていてよいゲートラベル。null はどれでもよい(連続していれば)。 */
const STAGE_GATES = {
  'stage:準備中': [[], ['gate0-approved']],
  'stage:着手待ち': [['gate0-approved', 'gate1-approved']],
  'stage:仕様確定': [GATES],
  'stage:実装中': [GATES],
  'stage:判断待ち': null,
};

const gatesOf = (labels) => GATES.filter((g) => labels.includes(g));

/**
 * ゲートラベルの裏付け(案 2)。付いているゲートラベルごとに、その工程の PR が main にマージされているか。
 * @param {number} n Issue の番号
 * @param {string[]} labels Issue のラベル
 * @param {string[]} mergedBranches main にマージされた PR の head のブランチ名
 * @returns {string[]} 指摘
 */
export function checkGateEvidence(n, labels, mergedBranches) {
  return Object.entries(GATE_EVIDENCE)
    .filter(([gate, role]) => labels.includes(gate) && !mergedBranches.some((b) => b.startsWith(`${role}/${n}-`)))
    .map(([gate, role]) => `${gate} が付いているが、${role}/${n}-… の PR が main にマージされていない(ゲートは、その工程の PR を人がマージして通す)`);
}

/**
 * Issue のラベルの組み合わせ全体(案 3。ラベルの付け外しのたびに確かめる)。
 * 列のラベルもゲートラベルも付いていない Issue(工場の改善など、ラインに乗らないもの)は見ない。
 * @returns {string[]} 指摘
 */
export function checkIssueLabels(n, labels, mergedBranches) {
  const stages = labels.filter((l) => l.startsWith('stage:'));
  const gates = gatesOf(labels);
  if (stages.length === 0 && gates.length === 0) return [];
  const out = [];
  // ゲートは飛ばさない: gate1 があるなら gate0、gate2 があるなら gate1 も要る
  for (let i = 1; i < GATES.length; i++) {
    if (labels.includes(GATES[i]) && !labels.includes(GATES[i - 1])) out.push(`${GATES[i]} が付いているが、${GATES[i - 1]} が付いていない(ゲートを飛ばしている)`);
  }
  if (stages.length === 0) out.push('列のラベル(stage:…)が付いていない');
  if (stages.length > 1) out.push(`列のラベルが ${stages.length} つ付いている(${stages.join('、')})。1 つにする`);
  if (stages.length === 1) {
    const allowed = STAGE_GATES[stages[0]];
    if (allowed === undefined) out.push(`${stages[0]} は工場の列のラベルではない`);
    else if (allowed !== null && !allowed.some((a) => a.length === gates.length && a.every((g) => gates.includes(g)))) {
      const want = allowed.map((a) => (a.length ? a.join('・') : 'ゲートラベルなし')).join(' か ');
      out.push(`${stages[0]} のときのゲートラベルは ${want}。いまは ${gates.length ? gates.join('・') : 'なし'}`);
    }
  }
  out.push(...checkGateEvidence(n, labels, mergedBranches));
  return out;
}

/**
 * 工程(ブランチの種類と同じ名前)。takt の workflow 名との対応も持つ。
 */
export const STAGES = {
  knowledge: { workflow: 'Knowledge', name: '[A] ナレッジ' },
  req: { workflow: 'Requirements', name: '[B] 要件・画面' },
  spec: { workflow: 'Spec', name: '[C] 仕様' },
  impl: { workflow: 'Implement', name: '[D] 実装' },
};

/** 列のラベルとゲートラベルの組み合わせ → 進めてよい工程。ここにない組み合わせは進めない。 */
const STAGE_TABLE = [
  { stage: 'stage:準備中', gates: [], role: 'knowledge' },
  { stage: 'stage:準備中', gates: ['gate0-approved'], role: 'req' },
  { stage: 'stage:着手待ち', gates: ['gate0-approved', 'gate1-approved'], role: 'spec' },
  { stage: 'stage:仕様確定', gates: GATES, role: 'impl' },
  { stage: 'stage:実装中', gates: GATES, role: 'impl' },
];

/**
 * Issue のラベルから、いま進めてよい工程を1つ選ぶ(tools/factory/takt.sh が使う)。
 * ゲートラベルの裏付け(マージ済みの PR)は CI の gate-check に任せ、ここでは見ない。
 * @param {string[]} labels Issue のラベル
 * @returns {{ role: string } | { role: null, reason: string }}
 */
export function selectStage(labels) {
  if (labels.includes('factory')) return { role: null, reason: '工場の改善の Issue(factory)はラインの列を使わない' };
  if (labels.includes('stage:判断待ち') || labels.includes('needs-answer')) return { role: null, reason: '人の回答待ち(stage:判断待ち か needs-answer がある)' };
  const stages = labels.filter((l) => l.startsWith('stage:'));
  if (stages.length !== 1) return { role: null, reason: `列のラベル(stage:…)がちょうど1つでない(${stages.join('、') || 'なし'})` };
  const gates = gatesOf(labels);
  const row = STAGE_TABLE.find((r) => r.stage === stages[0] && r.gates.length === gates.length && r.gates.every((g) => gates.includes(g)));
  if (!row) return { role: null, reason: `${stages[0]} とゲートラベル(${gates.join('・') || 'なし'})の組み合わせが表にない` };
  return { role: row.role };
}
