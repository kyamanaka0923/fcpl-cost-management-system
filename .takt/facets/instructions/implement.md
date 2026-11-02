工程 [D] 実装として、承認済みの仕様からテストとコードを書いてください。コミット・プッシュ・PR の作成はしません。

**最初に:**
- Report Directory の `triage.md` と `prepare.md` を読む。`git branch --show-current` が `impl/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `gate2-approved` と、`stage:実装中` があることを確かめる。なければ作業せずに止める
- `.factory/premises/ISSUE-<n>.yml` を読み、各前提の blob が `git rev-parse origin/main:<path>` と一致するかを確かめる。違えば、要件の穴として止める

**どの回かで作業を分ける:**
- 着手の初回: 処理ルール(`specs/rules/`)、`docs/api/openapi.json`、要件と `.feature`、業務ルールを読み、具体例の各行とシナリオを先にテストにしてから、層の順に実装する
- [D] の続きの初回(`triage.md` に既存の PR がある): `gh pr view <番号> --comments` と `gh pr checks <番号>` で、PR のレビューの指摘と CI の失敗を読み、1件ずつ対応する。CI の失敗は `gh run view <run-id> --log-failed` で原因を見る。対応しない指摘は理由をレポートに書く
- 検証の差し戻しのあと(直前の step が code-review): Report Directory の `code-review.md` の「直す」の指摘を1件ずつ直す。直さない指摘は理由をレポートに書く
- CI の失敗のあと(直前の step が ci-failed): Report Directory の `ci-failure.md` を読み、原因を直す

**書くとき:**
- 変えてよいのは `impl/` の持ち場(`backend/`・`frontend/`・`tests/`・`docs/`(`docs/REQUIREMENTS.md` と `docs/adr/` を除く)・`infra/`・`docker/`・`docker-compose.yml`・`README.md`・`.factory/premises/`)だけ
- この Issue の要件を指すシナリオの `@未実装` を外す。シナリオの文は変えない
- ドメインを変えるときは知識「ドメインの規約」の「ドメインを変えるときの手順」、DB スキーマを変えるときは知識「DB スキーマ変更の手順」に従う
- DTO・エンドポイントを変えたら OpenAPI と `frontend/src/api-schema.d.ts` を再生成し、仕様の `docs/api/openapi.json` と一致させる
- 前提に足すファイルを読んだら、`bash tools/factory/premises.sh <n> impl <ファイル>…` で前提を書き直す([C] の前提も含めて渡す)
- コミット前のチェックリストを実行し、結果(成功・失敗の件数、Skipped の件数)をレポートに書く。実行できなかったものは「未確認」と理由を書く
- 仕様の具体例・シナリオとテストの対応表をレポートに書く

**要件の穴を見つけたとき:**
- 仕様にないことを決めなければ進めない、仕様と要件・業務ルールが食い違う、前提の版と main の版が違う、のどれかに当たったら、作業を止める。自分で仕様を決めて進めない
- 問い(Yes/No か選択肢。各答えで何が起きるか)と、穴の場所(仕様・要件・業務ルールの ID、ファイル)をレポートに書く。Issue へのラベルとコメントは後の step がする
