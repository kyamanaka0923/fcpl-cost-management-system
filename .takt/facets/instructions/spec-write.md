工程 [C] 仕様として、承認済みの要件から処理ルール・OpenAPI の差分・テストの観点を書いてください。コミット・プッシュ・PR の作成はしません。

**最初に:**
- Report Directory の `triage.md` と `prepare.md` を読む。`git branch --show-current` が `spec/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `stage:着手待ち` と `gate1-approved` があることを確かめる。なければ作業せずに止める
- `.factory/premises/ISSUE-<n>.yml` を読み、前提のファイルを開く

**どの回かで作業を分ける:**
- 初回: 承認済みの要件(`requirements/`)と `.feature` のシナリオ、要件が参照する業務ルール、いまの `docs/api/openapi.json` を読み、`specs/rules/SPEC-xxx.md` と `docs/api/openapi.json` の差分を書く
- 検証の差し戻しのあと(直前の step が spec-review): Report Directory の `spec-review.md` の指摘を1件ずつ直す。直さない指摘は理由をレポートに書く
- CI の失敗のあと(直前の step が ci-failed): Report Directory の `ci-failure.md` を読み、この工程の変更が原因の失敗を直す。この工程の変更と関係のない失敗なら、変えずにその判断と根拠をレポートに書く

**書くとき:**
- 仕様の形式と置き場は `specs/README.md`・`specs/rules/README.md`・`specs/test-plan.md` に従う(書く前に読む)
- 変えてよいのは `specs/`、`docs/api/openapi.json`、`.factory/premises/` だけ
- 要件 1 件ごとに、満たす API と処理ルールを決め、対応表をレポートに書く
- 具体例の表に境界の値を入れ、各行のテストの観点を書く
- 前提に足すファイルを読んだら、`bash tools/factory/premises.sh <n> spec <ファイル>…` で前提を書き直す(前からある前提も含めて渡す)

**要件の穴を見つけたとき:**
- 満たし方が一通りに決まらない、要件や業務ルールにない決まりが要る、前提の版と main の版が違う、のどれかに当たったら、作業を止める。仕様に仮決めを書かない
- 問い(Yes/No か選択肢。各答えで何が起きるか)と、穴の場所(要件・業務ルールの ID、ファイル)をレポートに書く。Issue へのラベルとコメントは後の step がする
