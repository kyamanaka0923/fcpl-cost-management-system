工程 [A] ナレッジとして、Issue の内容から `knowledge/` を書いてください。コミット・プッシュ・PR の作成はしません(公開の step がします)。

**最初に:**
- タスクの本文(起動スクリプトが書いた Issue 番号・工程・ブランチ・既存の PR)と、Issue の本文・コメント(`gh issue view <n> --comments`)を読む。`git branch --show-current` が `knowledge/<n>-…` であることを確かめる。違えば作業せずに止める

**どの回かで作業を分ける:**
- 初回: Issue の本文とコメント、`docs/REQUIREMENTS.md`、`docs/requests/`、`docs/adr/`、既存の `knowledge/` を読み、用語・業務ルール・未確定事項・出どころを書く
- 検証の差し戻しのあと(直前の step が knowledge-review): Report Directory の `knowledge-review.md` の指摘を1件ずつ直す。直さない指摘は理由をレポートに書く
- 続き(タスクの「既存の PR」が番号)の初回: `gh pr view <番号> --comments` と `gh pr checks <番号>` で、PR のレビューの指摘と CI の失敗を読み、この工程の変更が原因のものを1件ずつ直す。CI の失敗は `gh run view <run-id> --log-failed` で原因を見る。この工程の変更と関係のない失敗・対応しない指摘は、変えずにその判断と根拠をレポートに書く

**書くとき:**
- ナレッジの形式と置き場は `knowledge/README.md` に従う(書く前に読む)
- 変えてよいのは `knowledge/` だけ
- 業務ルールの `sources`、`status`、問いの形、仮決めの禁止(金額・税の計算、権限・認可、個人情報)は persona の決まりに従う
- ナレッジに足すものがないと判断したら、ファイルを変えずにその理由をレポートに書く
- ゲート0で人に確かめてほしいこと(出どころとの一致、未確定事項の回答)を、Yes/No の問いの形でレポートに書く
