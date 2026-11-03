工程 [C] 仕様として、承認済みの要件から処理ルール・API の変更(`specs/api/ISSUE-<n>.openapi.json`)・テストの観点を書いてください。コミット・プッシュ・PR の作成はしません。

**最初に:**
- タスクの本文(起動スクリプトが書いた Issue 番号・工程・ブランチ・既存の PR)と、Issue の本文・コメント(`gh issue view <n> --comments`)を読む。`git branch --show-current` が `spec/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `stage:着手待ち` と `gate1-approved` があることを確かめる。なければ作業せずに止める
- 前提ファイル `.factory/premises/ISSUE-<n>.yml` がなければ作る。Issue が指す要件(`requirements/` の FR・`.feature` のシナリオ)と、その要件が参照する業務ルールを origin/main の中から特定し、`bash tools/factory/premises.sh <n> spec <ファイル>…`。直前の [B] の PR(マージ済み。`gh pr list --state merged --search "<n> in:body"`)の変更ファイルを手がかりにしてよい。スクリプトが「main にないファイル」で失敗したら、そのファイルを外さずに要件の穴として止める。前提にできるファイルが1つも特定できないときも止める
- 前提のファイルを開く

**どの回かで作業を分ける:**
- 初回: 承認済みの要件(`requirements/`)と `.feature` のシナリオ、要件が参照する業務ルール、いまの `docs/api/openapi.json` を読み、`specs/rules/SPEC-xxx.md` と、API を変えるなら `specs/api/ISSUE-<n>.openapi.json` を書く
- 検証の差し戻しのあと(直前の step が spec-review): Report Directory の `spec-review.md` の指摘を1件ずつ直す。直さない指摘は理由をレポートに書く
- 続き(タスクの「既存の PR」が番号)の初回: `gh pr view <番号> --comments` と `gh pr checks <番号>` で、PR のレビューの指摘と CI の失敗を読み、この工程の変更が原因のものを1件ずつ直す。CI の失敗は `gh run view <run-id> --log-failed` で原因を見る。この工程の変更と関係のない失敗・対応しない指摘は、変えずにその判断と根拠をレポートに書く
- 続き(タスクの「既存の PR」が「なし」で、「前の実行のコミット」が 1 件以上)の初回: 人の回答待ちで止めたときの書きかけから再開する。Issue の問いと、そのあとの人の回答(`gh issue view <n> --comments`)を読み、書きかけ(`git log --no-merges origin/main..HEAD` と `git diff "$(git merge-base origin/main HEAD)"`)を回答に合わせて直してから、残りを書き上げる。起動スクリプトが取り込んだ origin/main の変更(回答を受けて直した上流など)も読む。回答で決まらないことが残れば、もう一度要件の穴として止める

**書くとき:**
- 仕様の形式と置き場は `specs/README.md`・`specs/rules/README.md`・`specs/test-plan.md` に従う(書く前に読む)
- 変えてよいのは `specs/`、`.factory/premises/` だけ。`docs/api/openapi.json` は変えない([D] が実装から作り直す)
- API の変更は `specs/api/ISSUE-<n>.openapi.json` に書く。変える操作(パスとメソッド)とスキーマだけを、`docs/api/openapi.json` と同じ形・同じ書き方(いまの定義の `$ref`・`format` に合わせる)で丸ごと書く。消すものは `null`。書き方は `specs/README.md`
- API を変えたら `node tools/factory/cli.mjs api-spec` と `node tools/factory/cli.mjs screens` を実行する(どちらも、このブランチの仕様の API を当てて確かめる)。画面要件の fixtures が合わなければ要件の穴として止める
- 要件 1 件ごとに、満たす API と処理ルールを決め、対応表をレポートに書く
- 具体例の表に境界の値を入れ、各行のテストの観点を書く
- 前提に足すファイルを読んだら、`bash tools/factory/premises.sh <n> spec <ファイル>…` で前提を書き直す(前からある前提も含めて渡す)

**要件の穴を見つけたとき:**
- 満たし方が一通りに決まらない、要件や業務ルールにない決まりが要る、前提の版と main の版が違う、のどれかに当たったら、作業を止める。仕様に仮決めを書かない
- 問い(Yes/No か選択肢。各答えで何が起きるか)と、穴の場所(要件・業務ルールの ID、ファイル)をレポートに書く。Issue へのラベルとコメントは escalate の step がする
