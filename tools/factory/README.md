# 工場の道具(tools/factory/)

工場(ADR-0015)のゲートを機械で確かめる検査と、人が使う設定スクリプト。

| ファイル | 中身 |
|---|---|
| `cli.mjs` | ゲートの検査の入口。`node tools/factory/cli.mjs <検査名>`(リポジトリ直下で実行) |
| `checks/` | 検査の中身(入力を引数で受け取る純粋な関数) |
| `lib/` | 共通の部品(frontmatter・日本語 Gherkin の簡易パーサ・ブランチ名) |
| `test/` | 検査のテスト(`npm test` = `node --test`)。正例と反例を両方置く |
| `ambiguity-words.txt` | 曖昧語の一覧 |
| `premises.sh` | 前提の記録を作る(`.factory/premises/README.md`) |
| `setup-github.sh` | ラベルとルールセットを作る(人が実行する。エージェントはフックで止まる) |

## 検査

CI(`.github/workflows/factory.yml`)のジョブ名が、そのまま main のルールセットの必須チェックになる。

| ジョブ名 | 検査名 | 見ること | 落とす条件 |
|---|---|---|---|
| 要件の書式(EARS) | `ears` | `requirements/<領域>/FR-*.md` の frontmatter、ID とファイル名・領域、要件文の文型 | EARS の 5 文型の 1 文 1 応答でない、項目が欠けている |
| 曖昧語の検査 | `ambiguity` | 要件・業務ルール・`.feature` の `ルール:` 行 | 曖昧語が 1 つでもある |
| 要件とシナリオの網羅 | `coverage` | 要件 ↔ シナリオのトレーサビリティ(実行サマリと成果物 `coverage-matrix` に出す) | シナリオのない要件、孤児のタグ、`ルール:` の文が要件文と違う、1 ルールに 2 要件 |
| ナレッジの出どころと確定 | `knowledge` | 業務ルールの frontmatter と `sources`、要件が参照する業務ルール | 出どころがない・索引にない、未確定の業務ルールを要件が参照している |
| 工程の持ち場(role-guard) | `role` | PR のブランチ名と、変えたファイル | 命名に合わないブランチ、工程の持ち場の外の変更 |
| ゲートラベルの確認(gate-check) | `gate` | `req/`・`spec/`・`impl/` の PR の Issue のラベル | 前のゲートのラベル(gate0〜2-approved)がない |
| 前提の版(upstream-approved) | `premises` | `spec/`・`impl/` の PR の `.factory/premises/ISSUE-<n>.yml` | 前提の記録がない、前提のファイルが main で変わった・main にない |
| 工場の版の更新(factory-version) | `factory-version` | `.claude/`・`AGENTS.md`・`CLAUDE.md`・`.github/workflows/`・`tools/factory/` の変更 | `.factory/CHANGELOG.md` を変えていない |

- 下の 4 つは PR だけの検査。main への push では何もしない
- ゲートラベルは Issue に付くので、付けても PR の検査は動き直さない。付けたあとで「ゲートラベルの確認」を再実行する
- Dependabot のブランチ(`dependabot/…`)は、持ち場と工場の版の検査から外す

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
