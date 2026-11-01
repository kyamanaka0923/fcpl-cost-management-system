# 工場の道具(tools/factory/)

工場(ADR-0015)のゲートを機械で確かめる検査と、人が使う設定スクリプト。

| ファイル | 中身 |
|---|---|
| `cli.mjs` | ゲートの検査の入口。`node tools/factory/cli.mjs <検査名>`(リポジトリ直下で実行) |
| `ci-scope-cli.mjs`、`lib/ci-scope.mjs` | PR で変えた場所から、動かす CI のジョブを決める(#73。`ci.yml` と `factory.yml` の最初のジョブで使う。依存のパッケージなしで動く) |
| `cli.mjs changes`、`lib/changes.mjs` | 工場の変更の記録(`.factory/changes/`)を確かめて、Issue の番号の順の表を出す(#66) |
| `checks/` | 検査の中身(入力を引数で受け取る純粋な関数) |
| `lib/` | 共通の部品(frontmatter・日本語 Gherkin の簡易パーサ・ブランチ名) |
| `test/` | 検査のテスト(`npm test` = `node --test`)。正例と反例を両方置く |
| `ambiguity-words.txt` | 曖昧語の一覧 |
| `premises.sh` | 前提の記録を作る(`.factory/premises/README.md`) |
| `metrics-cli.mjs`、`lib/metrics.mjs` | 工場の週報(WIP・流れの時間・介入)を作る。`--post` で「工場の週報」Issue に足す |
| `intervention-cli.mjs`、`lib/intervention.mjs` | 介入の記録(`/intervention <種類> <原因>`)を確かめて `intervention` ラベルを付ける |
| `screens-docker.sh` | 画面の差分の検査を、CI と同じ Playwright のコンテナで動かす |
| `update-approved-screens.sh` | 承認する画面の画像を撮り直す(人が実行する。エージェントはフックで止まる) |
| `setup-github.sh` | ラベルとルールセットを作る(人が実行する。エージェントはフックで止まる) |

## 検査

CI(`.github/workflows/factory.yml`)のジョブ名が、そのまま main のルールセットの必須チェックになる。

| ジョブ名 | 検査名 | 見ること | 落とす条件 |
|---|---|---|---|
| 要件の書式(EARS) | `ears` | `requirements/<領域>/FR-*.md` の frontmatter、ID とファイル名・領域、要件文の文型 | EARS の 5 文型の 1 文 1 応答でない、項目が欠けている |
| 曖昧語の検査 | `ambiguity` | 要件・業務ルール・`.feature` の `ルール:` 行 | 曖昧語が 1 つでもある |
| 要件とシナリオの網羅 | `coverage` | 要件 ↔ シナリオのトレーサビリティ(実行サマリと成果物 `coverage-matrix` に出す) | シナリオのない要件、孤児のタグ、`ルール:` の文が要件文と違う、1 ルールに 2 要件、`impl/<n>` で Issue #n の要件のシナリオに `@未実装` が残っている |
| ナレッジの出どころと確定 | `knowledge` | 業務ルールの frontmatter と `sources`、要件が参照する業務ルール | 出どころがない・索引にない、未確定の業務ルールを要件が参照している |
| 画面要件と fixtures | `screens` | `requirements/screens/screens.yaml` の形、fixtures の body が OpenAPI の応答のスキーマに合うか | 項目の欠け、fixtures がない・余っている、スキーマに合わない、OpenAPI にない API |
| 工程の持ち場(role-guard) | `role` | PR のブランチ名と、変えたファイル | 命名に合わないブランチ、工程の持ち場の外の変更 |
| ゲートラベルの確認(gate-check) | `gate` | `req/`・`spec/`・`impl/` の PR の Issue のラベルと、main にマージされた PR | その工程までのゲートラベル(`spec/` なら gate0・gate1、`impl/` なら gate0〜2)が全部そろっていない。`gate1-approved` があるのに `req/<n>-…` が、`gate2-approved` があるのに `spec/<n>-…` がマージされていない(#77) |
| 前提の版(upstream-approved) | `premises` | `spec/`・`impl/` の PR の `.factory/premises/ISSUE-<n>.yml` | 前提の記録がない、前提のファイルが main で変わった・main にない |
| 作業項目を途中で閉じない(閉じるキーワード) | `closing` | `knowledge/`・`req/`・`spec/`・`prep/` の PR の本文 | 閉じるキーワード(close・fix・resolve の活用形)+ Issue の参照がある。否定の文の中でも落とす |
| 工場の版の更新(factory-version) | `factory-version` | `.claude/`・`AGENTS.md`・`CLAUDE.md`・`.github/workflows/`・`tools/factory/` の変更 | 変更の記録 `.factory/changes/<Issue番号>.md` を足していない、記録の frontmatter(`issue`・`kind`)が違う、記録を消した |

- このほかに、ジョブ「画面の差分(承認した画面との比較)」が `tests/e2e/screens` の Playwright を Playwright のコンテナで動かす(`requirements/screens/README.md`)
- 持ち場・ゲートラベル・前提の版・工場の版・閉じるキーワードの 5 つは PR だけの検査。main への push では何もしない。PR の本文を直すと(`edited`)検査が動き直す
- ゲートラベルは Issue に付くので、付けても PR の検査は動き直さない。付けたあとで「ゲートラベルの確認」を再実行する
- Dependabot のブランチ(`dependabot/…`)は、持ち場と工場の版の検査から外す

## ラベルの付け外しのたびの確認(#77)

`.github/workflows/factory-labels.yml`(`labels-cli.mjs`、判定は `lib/gate-labels.mjs`)。Issue に `stage:…` かゲートラベルを付け外ししたとき、
ゲートラベル・列のラベル・マージ済みの工程の PR の組み合わせが `factory-line` スキルの表どおりかを確かめ、表にないなら Issue にコメントで知らせる
(ラベルは付け外ししない)。あわせて、その Issue の開いている `req/`・`spec/`・`impl/` の PR の「工場のゲート」が失敗していれば、失敗したジョブを再実行する。

## 計測(週報と介入の記録)

| ワークフロー | いつ | すること |
|---|---|---|
| `factory-metrics.yml`(工場の週報) | 毎週月曜 09:00 JST と手動 | 直近 7 日の WIP・流れの時間(起票 → ゲート0〜2 → 完了の中央値)・マージした PR・介入を集め、`factory-report` ラベルの Issue にコメントで足す(なければ作る) |
| `factory-intervention.yml`(介入の記録) | Issue・PR に `/intervention` で始まるコメントが付いたとき | 形を確かめて `intervention` ラベルを付ける。形が違えば書き方を返し、手直しなら「工場の改善」の起票を促す |

- WIP の上限(判断待ち 5 件まで・未回答の問い 10 件まで・着手待ち 2 件以上)は `lib/metrics.mjs` の `WIP_LIMITS`。超えたら週報に ⚠️ が出る
- 介入として数えるのは、リポジトリの関係者(OWNER・MEMBER・COLLABORATOR)のコメントだけ
- 費用(Claude の利用量)は自動では集めない。週報の「手で記入する」欄に書く
- 手元で見るだけなら `node tools/factory/metrics-cli.mjs --days 30`(投稿しない)

## 手元で動かす

```bash
cd tools/factory && npm ci && npm test && cd ../..
node tools/factory/cli.mjs ears          # ears | ambiguity | coverage | knowledge
# PR の検査は、ブランチと比べるコミットを渡す
FACTORY_BRANCH=impl/46-x FACTORY_BASE=origin/main FACTORY_HEAD=HEAD node tools/factory/cli.mjs role
```

## 検査を足すとき

工場の変更の種類 B(導入計画 §11.2)。仕掛かり品があるときは、まず警告(落とさない)で入れ、流れ切ってから必須にする。

1. `checks/` に関数を書き、`test/` に正例と反例のテストを書く
2. `cli.mjs` の `CHECKS` に足し、`factory.yml` の matrix に 1 行足す
3. `setup-github.sh` の `wanted` にジョブ名を足す。PR をマージしたあとで人が実行し、必須チェックに加える(main で動いたものだけが加わる)
