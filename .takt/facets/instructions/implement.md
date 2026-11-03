工程 [D] 実装として、承認済みの仕様からテストとコードを書いてください。コミット・プッシュ・PR の作成はしません。

**最初に:**
- タスクの本文(起動スクリプトが書いた Issue 番号・工程・ブランチ・既存の PR)と、Issue の本文・コメント(`gh issue view <n> --comments`)を読む。`git branch --show-current` が `impl/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `gate2-approved` と、`stage:実装中` があることを確かめる。なければ作業せずに止める
- 前提ファイル `.factory/premises/ISSUE-<n>.yml` がこのブランチになければ作る。[C] の前提(main にあればその一覧)に、直前の [C] の PR の処理ルール・API の変更(`specs/api/ISSUE-<n>.openapi.json`。あれば)を足して、`bash tools/factory/premises.sh <n> impl <ファイル>…`。スクリプトが「main にないファイル」で失敗したら、要件の穴として止める
- `.factory/premises/ISSUE-<n>.yml` を読み、各前提の blob が `git rev-parse origin/main:<path>` と一致するかを確かめる。違えば、要件の穴として止める

**どの回かで作業を分ける:**
- 着手の初回: 処理ルール(`specs/rules/`)、API の変更(`specs/api/ISSUE-<n>.openapi.json`。あれば)といまの `docs/api/openapi.json`、要件と `.feature`、業務ルールを読み、具体例の各行とシナリオを先にテストにしてから、層の順に実装する
- 続き(タスクの「既存の PR」が番号)の初回: `gh pr view <番号> --comments` と `gh pr checks <番号>` で、PR のレビューの指摘と CI の失敗を読み、指摘と失敗を1件ずつ直す。CI の失敗は `gh run view <run-id> --log-failed` で原因を見る。この工程の変更と関係のない失敗・対応しない指摘は、変えずにその判断と根拠をレポートに書く
- 続き(タスクの「既存の PR」が「なし」で、「前の実行のコミット」が 1 件以上)の初回: 人の回答待ちで止めたときの書きかけから再開する。Issue の問いと、そのあとの人の回答(`gh issue view <n> --comments` の「## 回答」)を読み、書きかけ(`git log --no-merges origin/main..HEAD` と `git diff "$(git merge-base origin/main HEAD)"`)を回答に合わせて直してから、残りを書き上げる。起動スクリプトが取り込んだ origin/main の変更(回答を受けて直した上流など)も読む。回答で決まらないことが残れば、もう一度要件の穴として止める
- 検証の差し戻しのあと(直前の step が code-review): Report Directory の `code-review.md` の「直す」の指摘を1件ずつ直す。直さない指摘は理由をレポートに書く

**書くとき:**
- 変えてよいのは `impl/` の持ち場(`backend/`・`frontend/`・`tests/`・`docs/`(`docs/REQUIREMENTS.md` と `docs/adr/` を除く)・`infra/`・`docker/`・`docker-compose.yml`・`README.md`・`.factory/premises/`)だけ
- この Issue の要件を指すシナリオの `@未実装` を外す。シナリオの文は変えない
- ドメインを変えるときは知識「ドメインの規約」の「ドメインを変えるときの手順」、DB スキーマを変えるときは知識「DB スキーマ変更の手順」に従う
- DTO・エンドポイントを変えたら `docs/api/openapi.json` と `frontend/src/api-schema.d.ts` を実装から作り直す(手で書かない)。`specs/api/ISSUE-<n>.openapi.json` があれば `node tools/factory/cli.mjs api-spec` が指摘 0 件になるまで実装を合わせる。仕様のほうが誤りだと思ったら、仕様を直さずに要件の穴として止める
- 前提に足すファイルを読んだら、`bash tools/factory/premises.sh <n> impl <ファイル>…` で前提を書き直す([C] の前提も含めて渡す)
- コミット前のチェックリストを実行し、結果(成功・失敗の件数、Skipped の件数)をレポートに書く。実行できなかったものは「未確認」と理由を書く
- 仕様の具体例・シナリオとテストの対応表をレポートに書く

**要件の穴を見つけたとき:**
- 仕様にないことを決めなければ進めない、仕様と要件・業務ルールが食い違う、前提の版と main の版が違う、のどれかに当たったら、作業を止める。自分で仕様を決めて進めない
- 問い(Yes/No か選択肢。各答えで何が起きるか)と、穴の場所(仕様・要件・業務ルールの ID、ファイル)をレポートに書く。Issue へのラベルとコメントは escalate の step がする
