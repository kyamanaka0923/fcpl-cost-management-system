書いた成果物をコミット・プッシュし、Draft PR を作るか更新してください。成果物の中身は直しません。CI の結果は待ちません(CI の失敗は、人が同じコマンドをもう一度実行して「続き」で直します)。

**最初に:**
1. `bash tools/factory/guard/check-active.sh` を実行する。失敗したら(ゲートのガードがこのシェルの `gh`・`git` に効いていない)、何もせずに出力をレポートに書いて止める
2. タスクの本文(Issue 番号・工程・ブランチ・既存の PR)と、Report Directory のその工程の書く step のレポート(`knowledge-write.md`・`requirements-write.md` と `screen-write.md`・`spec-write.md`・`implement.md` のうち、その工程のもの)、検証のレポート(`knowledge-review.md`・`requirements-review.md`・`spec-review.md`・`code-review.md` のうち、その工程のもの)を読む
3. 検証の結果を確かめる
   - 「指摘なし」: ふつうに公開する
   - 指摘が残っていて、前の step の出力の1行目が `上限: 検証の差し戻し`: 差し戻しの上限に達した。止めずに、残った指摘を添えて公開する(下の「上限に達したとき」)
   - それ以外: 公開せずに止める
4. `git branch --show-current` がタスクのブランチであることを確かめる。違えば止める

**変更がないとき([A] だけ):**
- [A] で、`git status --porcelain` が空、`git rev-list --count origin/main..HEAD` が 0、その工程の PR もないときは、PR を作らない
- Issue に「[A] ナレッジ: 足すものがなかった」こと、その理由(書く step と検証のレポートから)、ゲート0 は人が判定することをコメントして終える
- [A] 以外で変更がないときは、既存の PR があれば、コミットせずに下の「PR」の手順で説明と検証結果を更新する。PR もなければ公開せずに止める

**コミットとプッシュ:**
1. `git status --porcelain` と `git diff --name-only "$(git merge-base origin/main HEAD)"` の変更ファイルが、すべてその工程の持ち場(`ROLES`)に入っていることを確かめる。外のファイルがあれば公開せずに止める
2. 変更ファイルを名前で指定して `git add` し、日本語のコミットメッセージ(「何を・なぜ」。作業項目は `Refs #<n>` と書く)で `git commit` する
3. `git push -u origin <ブランチ>` を、PR を作るコマンドとは別に実行する。ネットワークエラーのときだけ指数バックオフで最大4回やり直す。拒否されたら(ガード・ルールセット・権限)回避せずに止める
4. プッシュした先頭のコミット(`git rev-parse HEAD`)を控える

**PR:**
1. PR の本文を `$TMPDIR` のファイルに書く。`.github/pull_request_template.md` の欄をすべて埋める(当てはまらない欄は「なし」)
   - 工程: ブランチの種類と [A]〜[D] の名前
   - 作業項目の参照: `impl/` なら `Closes #<n>`、それ以外なら `Refs #<n>`
   - 何をしたか: 書く step のレポートと差分から
   - 前提: `.factory/premises/ISSUE-<n>.yml` と、前提にした要件・仕様・ナレッジ([A][B] は「なし」)
   - 工場の版: `.factory/VERSION` の中身と `git log -1 --format=%h -- .takt/`(テンプレートの「工場(`.takt/`)の最終コミット」)
   - 実行環境: takt の workflow(タスクの工程の workflow 名)で動いたこと、エージェント(環境変数 `FACTORY_AGENT` の値。claude・codex・copilot)、実行場所
   - レビューView: 事実(件数・数値・確かめた方法。この実行で検証が差し戻した回数)・理由(根拠の ID)・問い(Yes/No か選択肢、3 つまで)。書き方は知識の「レビューView」とゲートごとの確認観点に従う
   - [B] で画面の変更があるとき: 問いに「この状態の画面を撮って承認してほしい(画面 ID・状態)」を入れる。承認画像はこの workflow では撮らない
   - 介入の記録: 人が記録した介入だけ。なければ「なし」
2. 本文に閉じる書き方が `impl/` の `Closes #<n>` 以外にないことを、ファイルを読んで確かめる。CI の検査(`tools/factory/checks/closing.mjs`)と同じ形で探す: `grep -inP '\b(close[sd]?|fix(?:e[sd])?|resolve[sd]?)\b:?\s+((?:[\w.-]+/[\w.-]+)?#\d+|https://github\.com/[\w.-]+/[\w.-]+/issues/\d+)' <ファイル>`。検証の結果のコメント・Issue へのコメントの本文も同じように確かめる
3. PR を作るか更新する
   - その工程の PR がない: `gh pr create --draft --base main --head <ブランチ> --title "<日本語の題>" --body-file <ファイル>`
   - PR がある(タスクの「既存の PR」、またはこの実行で前に作った PR): `gh pr edit <番号> --body-file <ファイル>`
4. 検証のレポートを、元の検証役のコメントの形(`## 要件のレビュー(requirements-reviewer)` などの見出し、指摘・確かめた観点・人に決めてほしいこと)に整え、プッシュした先頭のコミットを添えて `gh pr comment <番号> --body-file <ファイル>` で1件書く。検証の途中の差し戻しの経過は書かない
5. `gh pr view <番号> --json isDraft,url,headRefOid` で、Draft であることと、先頭のコミットがプッシュしたものと同じことを確かめる
6. この実行で初めて PR を作ったときだけ、Issue に「どの工程の PR を出したか(番号)・事実の要約・人に決めてほしいこと」を日本語でコメントする(`gh issue comment <n> --body-file <ファイル>`)。CI の結果を見て、落ちていたらもう一度 `tools/factory/takt.sh <n>` を実行すれば続きで直すことも書く

**上限に達したとき:**
- PR のタイトルの先頭に「⚠️ 未解決の指摘あり」を付ける(既存の PR なら `gh pr edit <番号> --title …` で付ける)
- レビューView の「事実」の先頭に、差し戻しを3回直しても4回目の検証で指摘が残ったことと、「未解決の指摘」の表(対象・場所・内容・根拠。検証のレポートから写し、言い換えで中身を変えない)を書く
- 「問い」に、残った指摘をどう扱うか(受け入れる / 上流のナレッジ・要件・仕様・工場のどこを直す / もう一度実行して直させる)を入れる

マージ・Ready 化・承認・ゲートラベルの付け外しはしない。
