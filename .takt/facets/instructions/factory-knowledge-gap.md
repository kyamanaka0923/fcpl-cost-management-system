工程 [B] の土台になる業務ルールが確定していないため、この実行を止めます。Issue に問いと人がすることを報告し、何も入っていないブランチを片付けてください。成果物は書かず、コミット・プッシュ・PR の作成・ラベルの変更もしません。

**手順:**

1. Report Directory を読む
   - `triage.md`: Issue 番号、「業務ルールの確定」の行
   - あれば `prepare.md`(作ったブランチと、切り替える前のブランチ)と `requirements-write.md`(「人への問い」)
   - どちらから来たか(triage か requirements-write か)をレポートの「止めた step」に書く
2. 未確定の業務ルールを確かめ直す。ID ごとに `gh api 'repos/{owner}/{repo}/contents/knowledge/business-rules/<ID>.md?ref=main' -H 'Accept: application/vnd.github.raw'` の `status:` と、本文の「まだ決まっていないこと」に挙がった `OQ-…` を読む。`knowledge/open-questions.md` も同じく main から読み、その OQ の回答欄が空かを見る
3. 同じ報告が既にあるかを確かめる。`gh issue view <n> --json comments --jq '.comments[].body'` に `<!-- factory:knowledge-gap <ID の並び> -->` (ID は昇順、空白区切り)を含むコメントがあれば、新しく書かない。そのことと、そのコメントの URL(`gh issue view <n> --json comments --jq '.comments[] | select(.body | contains("factory:knowledge-gap")) | .url'`)をレポートに書いて手順 5 へ進む
4. コメントを `$TMPDIR` のファイルに日本語で書き、`gh issue comment <n> --body-file <ファイル>` で出す
   - 1 行目に `<!-- factory:knowledge-gap <ID の並び> -->`
   - [B] 要件・画面に進めなかったこと。理由は、要件の土台になる業務ルールが main で `status: 確定` でないこと(ID ごとの status)
   - 回答が要る問い: 未回答の `OQ-…` ごとに、問いの中身と選択肢・仮決めを `knowledge/open-questions.md` から写す(言い換えで中身を変えない)
   - 人がすること(この順):
     1. 上の問いに回答する(Issue へのコメントでよい)
     2. Issue から `gate0-approved` を外す(付いているあいだは判定表が [B] を選ぶため、[A] をやり直せない)
     3. `takt --pipeline --skip-git -w Software-Factory -t "#<n>"` を実行する。[A] で回答を業務ルールに移し、`status: 確定` にする
     4. その PR をマージし、`gate0-approved` を付け直してから、もう一度実行する([B] に進む)
   - ゲートラベルを付け外すのは人であること。この workflow はラベルを変えていないこと
   - 閉じるキーワードと `#番号` を続けて書かない。`/intervention` を書かない
5. ブランチを片付ける(`prepare.md` でブランチを作ったときだけ)
   - いまのブランチが `prepare.md` で作ったブランチで、次がすべて成り立つときだけ、切り替える前のブランチに戻して消す
     - `git status --porcelain` が空
     - `git rev-list --count origin/main..HEAD` が 0(コミットがない)
     - `git ls-remote --heads origin <ブランチ>` が空(プッシュしていない)
   - 戻して消す: `git switch <切り替える前のブランチ>` のあと `git branch -d <ブランチ>`。`-D` は使わない(`-d` が失敗したら消さずにレポートに書く)
   - 切り替える前のブランチが `prepare.md` にないときは `main` に戻す
   - 1 つでも成り立たないときは、何も消さずにブランチと残っている変更をレポートに書く
