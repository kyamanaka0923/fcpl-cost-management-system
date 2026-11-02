工程 [A] ナレッジの検証として、このブランチの `knowledge/` の差分を確かめてください。ファイルは書き換えません。

**手順:**

1. タスクの本文と Issue(`gh issue view <n> --comments`)を読み、Report Directory の `knowledge-write.md` で書く step の報告を読む(報告は手がかりで、判定は実物で行う)。ナレッジの形式の決まりとして `knowledge/README.md` も読む
2. 差分を読む: `git diff "$(git merge-base origin/main HEAD)"`、`git status --porcelain`(未追跡のファイルは中身を開く)
3. 差分の業務ルール・用語・問いを1件ずつ、次の4点で確かめる
   - 出どころが書かれているか: `sources` の ID が `knowledge/sources.md` にあり、その出どころ(Issue の文・資料・ADR・コード)を開くと同じことが書いてあるか
   - 推測で埋めていないか: 出どころにないことがルールや用語になっていないか。この工程で書いたルールが `status: 確定` になっていないか
   - 問いが `knowledge/open-questions.md` に起票されているか: Issue と差分から読み取れる分からないことに、ID・問い・選択肢・仮決め・理由・外れたら直す範囲のある問いがあるか
   - 金額・税・権限・個人情報を仮決めしていないか
4. `knowledge/` の外のファイルが変わっていないか、`tools/factory/ambiguity-words.txt` の語を使っていないか、ポリシー「レビューの観点」の「共通」の観点を確かめる
5. 2回目以降は、前回の `knowledge-review.md` の指摘が直ったかを1件ずつ確かめる
6. 書く step が「足すものがない」と判断したときは、その判断が Issue の内容と既存の `knowledge/` から見て正しいかを確かめる
