書く step が、人の回答なしには進めないと判断して止めました。Issue に問いを出して、この実行を終えてください。成果物は直さず、コミット・プッシュ・PR の作成もしません。ゲートラベルは触りません。

**最初に:**
1. `bash tools/factory/guard/check-active.sh` を実行する。失敗したら、何もせずに出力をレポートに書いて止める(「問いを出せなかった」)
2. タスクの本文(Issue 番号・工程・ブランチ)と、前の step の出力、Report Directory の書く step のレポートを読む
   - [B] の `requirements-write.md` の「人への問い」(要件の土台のナレッジが未確定・足りない)
   - [C] の `spec-write.md`、[D] の `implement.md` の「要件の穴」
3. ナレッジが未確定のときは、ID ごとに `gh api 'repos/{owner}/{repo}/contents/knowledge/business-rules/<ID>.md?ref=main' -H 'Accept: application/vnd.github.raw'` の `status:` と、本文の「まだ決まっていないこと」に挙がった `OQ-…` を確かめ直す。問いの中身と選択肢・仮決めは main の `knowledge/open-questions.md` から写す(言い換えで中身を変えない)

**ラベル(どの工程でも同じ):**
1. `gh issue view <n> --json labels` でいまの列のラベル(`stage:…`)を読む
2. `gh issue edit <n> --remove-label <いまの列のラベル> --add-label stage:判断待ち --add-label needs-answer` を、1つのコマンドで、他のコマンドとつながずに実行する
3. `gh issue view <n> --json labels` で、`needs-answer` と `stage:判断待ち` が付き、列のラベルが `stage:判断待ち` だけになったことを確かめる

**コメント:**
- 本文を `$TMPDIR` のファイルに日本語で書き、`gh issue comment <n> --body-file <ファイル>` で出す。コメントの URL をレポートに書く
- 書くこと
  - どの工程で止めたか([B] 要件・画面 / [C] 仕様 / [D] 実装)と、ブランチ名
  - 止めた理由: ナレッジが未確定(業務ルールの ID ごとの status)か、要件の穴(要件・業務ルール・仕様の ID とファイル、なぜ一通りに決まらないか)
  - 問い: Yes/No か選択肢で答えられる形。各答えで何が起きるかも書く
  - 人がすること: 問いに回答し、`needs-answer` と `stage:判断待ち` を外して列を戻し、`tools/factory/takt.sh <n>` をもう一度実行する。戻す列は次のとおり
    - 要件の穴: 外した列(`stage:着手待ち` や `stage:実装中`)に戻す。問いを出した工程からやり直す
    - ナレッジが未確定: `stage:準備中` に戻し、`gate0-approved` を外す([A] で回答を業務ルールに移して `status: 確定` にする)。その PR をマージしたら `gate0-approved` を付け直す([B] に進む)
  - ゲートラベルを付け外すのは人で、この workflow はゲートラベルを変えていないこと
- 閉じるキーワード(close・fix・resolve の活用形)と `#番号` を続けて書かない。`/intervention` を書かない

**残すもの:**
- 作業ツリーの書きかけの変更とブランチはそのままにし、プッシュしない。残っているものをレポートに書く(push していないブランチは、次に `tools/factory/takt.sh` を実行したときに、コミットがなければ origin/main から作り直される)
