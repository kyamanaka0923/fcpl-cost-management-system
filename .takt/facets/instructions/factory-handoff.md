検証を通った成果物を、人のゲート判定に引き渡す準備をしてください。成果物の中身は直しません。コミット・push・PR の作成・PR の本文の編集・PR へのコメントはしません(workflow が COMPLETE で終わったあとに takt本体がコミット・push・Draft PR の作成をし、PR ができたあとで人がこの step の案を貼ります)。

`gh` には必ず `-R kyamanaka0923/fcpl-cost-management-system` を付けます。`origin/…` は使いません。本文の案・コメントの案はリポジトリの外(`$TMPDIR`)のファイルに書きます(リポジトリの中に書くと takt本体がコミットしてしまう)。

**最初に:**
1. Report Directory の `triage.md`(Issue 番号・工程・タスクのブランチ・既存の PR)、`prepare.md`、その工程の書く step のレポート(`knowledge-write.md`・`requirements-write.md` と `screen-write.md`・`spec-write.md`・`implement.md` のうち、その工程のもの)、検証のレポート(`knowledge-review.md`・`requirements-review.md`・`spec-review.md`・`code-review.md` のうち、その工程のもの)を読む
2. 検証のレポートの結果が「指摘なし」であることを確かめる。違えば引き渡さずに止める
3. `git branch --show-current` が `triage.md` のタスクのブランチであることを確かめる。違えば止める

**変更の一覧:**
- この実行の変更: `git status --porcelain`(変更・削除・未追跡のすべて。takt本体はこれをすべてコミットする)
- [D] の続きは、既存の PR の変更も `gh pr diff <番号> -R kyamanaka0923/fcpl-cost-management-system --name-only` で読む

**変更がないとき:**
- [A] で `git status --porcelain` が空のときは、PR を作らない(takt本体はコミットがないと PR を作らない)
  - Issue に「[A] ナレッジ: 足すものがなかった」こと、その理由(書く step と検証のレポートから)、ゲート0 は人が判定することを、`$TMPDIR` のファイルに日本語で書く。閉じるキーワードと `#番号` を続けて書いていないことを下の「閉じるキーワードの確認」と同じ方法で確かめてから、`gh issue comment <n> -R kyamanaka0923/fcpl-cost-management-system --body-file <ファイル>` で出す
  - コメントの URL をレポートに書いて終える(「[A] で変更がなく、Issue へ報告した」)。出せなければ止める
- [A] 以外で `git status --porcelain` が空のとき
  - [D] の続き: takt本体は新しいコミットがないので PR にコメントしない。下の「案を書く」を行い、変更がなかったことをレポートに書いて終える(人が案を既存の PR に貼る)
  - [B][C][D] の着手: 引き渡すものがないので止める

**持ち場の確認:**
- 変更の一覧のファイルが、すべてそのブランチの種類の持ち場(`tools/factory/checks/branch.mjs` の `ROLES`。知識の「工程ごとの置き場とブランチ」の表)に入っていることを確かめる。allow のどれかに当たり、deny のどれにも当たらないファイルだけがよい
- 持ち場の外のファイルが1つでもあれば、引き渡さずに止める。ファイルの一覧をレポートに書く(直さない・消さない)

**閉じるキーワードの確認:**
- 探し方は知識の「作業項目の参照」の `grep -inP …`(CI の検査 `tools/factory/checks/closing.mjs` と同じ形)
- 確かめるもの
  - タスクの本文(takt の PR の本文に入る)。本文は Report Directory の外にあるので、この step に渡されたタスクの本文を `$TMPDIR` のファイルに写して確かめる
  - 下で書く PR 本文の案(`impl/` の `Closes #<n>` の1行だけはよい)と、検証結果のコメントの案
  - この実行で変えたファイル(閉じるキーワードと `#番号` が書かれていないか)
- 見つかったら、引き渡さずに止める。見つかった場所と行をレポートに書く

**案を書く:**
1. PR 本文の案を `$TMPDIR` のファイルに書く。`.github/pull_request_template.md` の欄をすべて埋める(当てはまらない欄は「なし」)
   - 工程: ブランチの種類と [A]〜[D] の名前
   - 作業項目の参照: `impl/` なら `Closes #<n>`、それ以外なら `Refs #<n>`
   - 何をしたか: 書く step のレポートと差分から
   - 前提: `.factory/premises/ISSUE-<n>.yml` と、前提にした要件・仕様・ナレッジ([A][B] は「なし」)
   - 工場の版: `.factory/VERSION` の中身と `git log -1 --format=%h -- .claude/`
   - 実行環境: takt の workflow `Software-Factory` を、リポジトリの checkout で `--pipeline --skip-git` で動かしたこと、実行場所
   - レビューView: 事実(件数・数値・確かめた方法。この実行で検証が差し戻した回数を数えられた範囲で書き、数えられなければ「未確認」。CI はこの実行では見ていないので「未確認(PR ができたあとに動く)」と書く)・理由(根拠の ID)・問い(Yes/No か選択肢、3 つまで)。書き方は知識の「レビューView」とゲートごとの確認観点に従う
   - [B] で画面の変更があるとき: 問いに「この状態の画面を撮って承認してほしい(画面 ID・状態)」を入れる。承認画像はこの workflow では撮らない
   - 介入の記録: 人が記録した介入だけ。なければ「なし」
2. 検証結果のコメントの案を `$TMPDIR` のファイルに書く。検証のレポートを、元の検証役のコメントの形(`## 要件のレビュー(requirements-reviewer)` などの見出し、指摘・確かめた観点・人に決めてほしいこと)に整え、検証した HEAD の SHA と「未コミットの変更を検証した。コミットは takt本体がする」ことを添える。検証の途中の差し戻しの経過は書かない
3. 2つの案に閉じるキーワードがないことを、上の方法で確かめる
4. 2つの案の全文を、この step の結果にそのまま書く(レポートに写すため)

マージ・Ready 化・承認・ゲートラベルの付け外しはしない。PR が既にあっても、PR の本文の編集とコメントはしない。
