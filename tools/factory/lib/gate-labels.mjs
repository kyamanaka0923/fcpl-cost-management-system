// 作業項目(Issue)のゲートラベル・列のラベルと、マージ済みの工程の PR の組み合わせを確かめる(#77)。
// 人がゲートラベルを付け間違えて工程を飛ばしても、CI とラベルの付け外しのたびの検査で止める。
// 正しい組み合わせの表は .claude/skills/factory-line/SKILL.md の「1.」。

export const GATES = ['gate0-approved', 'gate1-approved', 'gate2-approved'];

/** 工程が着手するのに要るゲートラベル(その工程までのすべて)。 */
export const REQUIRED_GATES = {
  req: ['gate0-approved'],
  spec: ['gate0-approved', 'gate1-approved'],
  impl: ['gate0-approved', 'gate1-approved', 'gate2-approved'],
};

/**
 * ゲートラベルの裏付けになる、マージ済みの工程の PR のブランチの種類。
 * ゲート0 は、ナレッジに足すものがない作業項目では PR なしで付く(factory-line スキル)ので求めない。
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
