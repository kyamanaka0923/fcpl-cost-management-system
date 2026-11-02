検証を通った成果物をコミット・プッシュし、Draft PR を作るか更新してください。成果物の中身は直しません。

**最初に:**
1. Report Directory の `start.md`(Issue 番号・工程・既存の PR・ブランチ)、その工程の書く step のレポート(`knowledge-write.md`・`requirements-write.md` と `screen-write.md`・`spec-write.md`・`implement.md` のうち、その工程のもの)、検証のレポート(`knowledge-review.md`・`requirements-review.md`・`spec-review.md`・`code-review.md` のうち、その工程のもの)を読む
2. 検証のレポートの結果が「指摘なし」であることを確かめる。違えば公開せずに止める
3. `git branch --show-current` がその工程のブランチであることを確かめる。main や別の工程のブランチなら止める

**変更がないとき([A] だけ):**
- [A] で、`git status --porcelain` が空、`git rev-list --count origin/main..HEAD` が 0、その工程の PR もないときは、PR を作らない
- Issue に「[A] ナレッジ: 足すものがなかった」こと、その理由(書く step と検証のレポートから)、ゲート0 は人が判定することをコメントして終える
- [A] 以外で変更がないときは、既存の PR があれば、コミットせずに下の「PR」の手順で説明と検証結果を更新する。PR もなければ公開せずに止める

**コミットとプッシュ:**
1. `git status --porcelain` と `git diff --name-only "$(git merge-base origin/main HEAD)"` の変更ファイルが、すべてその工程の持ち場(`ROLES`)に入っていることを確かめる。外のファイルがあれば公開せずに止める
2. 変更ファイルを名前で指定して `git add` し、日本語のコミットメッセージ(「何を・なぜ」。作業項目は `Refs #<n>` と書く)で `git commit` する
3. `git push -u origin <ブランチ>` を、PR を作るコマンドとは別に実行する。ネットワークエラーのときだけ指数バックオフで最大4回やり直す。拒否されたら(フック・ルールセット・権限)回避せずに止める
4. プッシュした先頭のコミット(`git rev-parse HEAD`)を控える

**PR:**
1. PR の本文を `$TMPDIR` のファイルに書く。`.github/pull_request_template.md` の欄をすべて埋める(当てはまらない欄は「なし」)
   - 工程: ブランチの種類と [A]〜[D] の名前
   - 作業項目の参照: `impl/` なら `Closes #<n>`、それ以外なら `Refs #<n>`
   - 何をしたか: 書く step のレポートと差分から
   - 前提: `.factory/premises/ISSUE-<n>.yml` と、前提にした要件・仕様・ナレッジ([A][B] は「なし」)
   - 工場の版: `.factory/VERSION` の中身と `git log -1 --format=%h -- .claude/`
   - 実行環境: takt の workflow `Software-Factory` で動いたこと、エージェント(環境変数 `FACTORY_AGENT` の値。claude・codex・copilot)、実行場所
   - レビューView: 事実(件数・数値・確かめた方法。この実行で検証が差し戻した回数と CI を直した回数を数えられた範囲で書き、数えられなければ「未確認」)・理由(根拠の ID)・問い(Yes/No か選択肢、3 つまで)。書き方は知識の「レビューView」とゲートごとの確認観点に従う
   - [B] で画面の変更があるとき: 問いに「この状態の画面を撮って承認してほしい(画面 ID・状態)」を入れる。承認画像はこの workflow では撮らない
   - 介入の記録: 人が記録した介入だけ。なければ「なし」
2. 本文に閉じる書き方が `impl/` の `Closes #<n>` 以外にないことを、ファイルを読んで確かめる。CI の検査(`tools/factory/checks/closing.mjs`)と同じ形で探す: `grep -inP '\b(close[sd]?|fix(?:e[sd])?|resolve[sd]?)\b:?\s+((?:[\w.-]+/[\w.-]+)?#\d+|https://github\.com/[\w.-]+/[\w.-]+/issues/\d+)' <ファイル>`。検証の結果のコメント・Issue へのコメントの本文も同じように確かめる
3. PR を作るか更新する
   - その工程の PR がない: `gh pr create --draft --base main --head <ブランチ> --title "<日本語の題>" --body-file <ファイル>`
   - PR がある([D] の続き、またはこの実行で前に作った PR): `gh pr edit <番号> --body-file <ファイル>`
4. 検証のレポートを、元の検証役のコメントの形(`## 要件のレビュー(requirements-reviewer)` などの見出し、指摘・確かめた観点・人に決めてほしいこと)に整え、プッシュした先頭のコミットを添えて `gh pr comment <番号> --body-file <ファイル>` で1件書く。検証の途中の差し戻しの経過は書かない
5. `gh pr view <番号> --json isDraft,url,headRefOid` で、Draft であることと、先頭のコミットがプッシュしたものと同じことを確かめる
6. この実行で初めて PR を作ったときだけ、Issue に「どの工程の PR を出したか(番号)・事実の要約・人に決めてほしいこと」を日本語でコメントする(`gh issue comment <n> --body-file <ファイル>`)

マージ・Ready 化・承認・ゲートラベルの付け外しはしない。
