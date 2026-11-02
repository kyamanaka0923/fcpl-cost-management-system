この実行は、ここで人に渡して止めます。止めた理由を決め、Issue に報告してください。成果物は直さず、コミット・プッシュ・PR の作成もしません。ゲートラベルは触りません。

**1. 止めた理由を決める**

前の step の出力と Report Directory のレポートから、次の4つのどれかを決める。前の step の名前をレポートの「止めた step」に書く。

| 前の step | 理由 | 読むレポート |
|---|---|---|
| start(判定が「ナレッジが未確定」)、requirements-write(「要件の土台のナレッジが未確定・足りない」) | ナレッジが未確定 | `start.md`、あれば `requirements-write.md` の「人への問い」 |
| spec-write、implement(要件の穴で止めた) | 要件の穴 | `start.md`、`spec-write.md` か `implement.md` の「要件の穴」 |
| 上限の判定(出力の1行目が `上限: 検証の差し戻し`) | 検証の差し戻しが上限に達した | `start.md`、その工程の検証のレポート(`knowledge-review.md`・`requirements-review.md`・`spec-review.md`・`code-review.md` のうち、その工程のもの)の最新の指摘 |
| 上限の判定(出力の1行目が `上限: CI の手直し`) | CI の手直しが上限に達した | `start.md`、`publish.md`(PR 番号)、`ci-wait.md`、`ci-failure.md`。最新の失敗のログを `gh run view <run-id> --log-failed` で確かめる |

前の step の出力で決まらないときは、Report Directory のレポートを更新の新しい順(`ls -t`)に見て、最後に書かれた書く step・検証・CI のレポートから決める。それでも決まらないときは、Issue にコメントせず「報告できなかった」を選ぶ。

**2. 理由ごとにすること**

- ナレッジが未確定
  1. 未確定の業務ルールを確かめ直す。ID ごとに `gh api 'repos/{owner}/{repo}/contents/knowledge/business-rules/<ID>.md?ref=main' -H 'Accept: application/vnd.github.raw'` の `status:` と、本文の「まだ決まっていないこと」に挙がった `OQ-…` を読む。`knowledge/open-questions.md` も同じく main から読み、その OQ の回答欄が空かを見る
  2. 同じ報告が既にあるかを確かめる。`gh issue view <n> --json comments --jq '.comments[] | select(.body | contains("factory:knowledge-gap <ID の並び>")) | .url'` が URL を返したら、新しく書かない。その URL をレポートに書いて 3 へ進む
  3. コメントの1行目に `<!-- factory:knowledge-gap <ID の並び> -->`(ID は昇順、空白区切り)。本文は、[B] 要件・画面に進めなかったことと、その理由(ID ごとの status)、回答が要る問い(未回答の `OQ-…` ごとに、問いの中身と選択肢・仮決めを `knowledge/open-questions.md` から写す。言い換えで中身を変えない)、人がすること(この順):
     1. 上の問いに回答する(Issue へのコメントでよい)
     2. Issue から `gate0-approved` を外す(付いているあいだは判定表が [B] を選ぶため、[A] をやり直せない)
     3. `tools/factory/takt.sh <n>` を実行する。[A] で回答を業務ルールに移し、`status: 確定` にする
     4. その PR をマージし、`gate0-approved` を付け直してから、もう一度実行する([B] に進む)
  4. ラベルは変えない(ゲートラベルを付け外すのは人)
  5. ブランチを片付ける(`start.md` でブランチを作ったときだけ)。いまのブランチが `start.md` で作ったブランチで、`git status --porcelain` が空、`git rev-list --count origin/main..HEAD` が 0、`git ls-remote --heads origin <ブランチ>` が空のときだけ、`git switch <切り替える前のブランチ>`(`start.md` になければ `main`)のあと `git branch -d <ブランチ>`。`-D` は使わない。1 つでも成り立たないときは、何も消さずにブランチと残っている変更をレポートに書く

- 要件の穴
  1. `gh issue view <n> --json labels` でいまの列のラベル(`stage:…`)を読み、`gh issue edit <n> --remove-label <いまの列のラベル> --add-label stage:判断待ち --add-label needs-answer` を、1つのコマンドで、他のコマンドとつながずに実行する
  2. コメントには、どの工程で止めたか([C] 仕様 / [D] 実装)、ブランチ名、外した列のラベル(回答のあとに戻す列)、穴の場所(要件・業務ルール・仕様の ID、ファイル)と、なぜ一通りに決まらないか、問い(Yes/No か選択肢。各答えで何が起きるか)、人が回答したあと問いを出した工程からやり直すこと、を書く
  3. `gh issue view <n> --json labels` で、`needs-answer` と `stage:判断待ち` が付き、列のラベルが `stage:判断待ち` だけになったことを確かめる
  4. 作業ツリーに残った書きかけの変更はそのままにし、プッシュしない

- 検証の差し戻しが上限に達した
  - コメントには、どの工程か、検証の差し戻しを3回直しても4回目の検証で指摘が残り上限に達して止めたこと、残った指摘の表(対象・場所・内容・根拠。検証のレポートから写し、言い換えで中身を変えない)、ブランチ名とプッシュしていないこと(既に PR があるときは、その PR は前の公開のままであること)、人に決めてほしいこと(例: 指摘のどれを受け入れるか、上流のどこを直すか)を書く
  - ラベルは変えない

- CI の手直しが上限に達した
  - コメントには、どの工程の PR(番号)か、CI の失敗を3回直しても4回目の CI が失敗し上限に達して止めたこと、失敗している必須チェックごとの名前・リンク・エラーの要点(メッセージ、ファイルと行、失敗したテスト)、PR は Draft のまま残っていること、人に決めてほしいこと(例: CI の再実行や例外の判断をするか、上流のどこを直すか)を書く
  - ラベルは変えない

**3. コメントの出し方(どの理由でも)**

- 本文を `$TMPDIR` のファイルに日本語で書き、`gh issue comment <n> --body-file <ファイル>` で出す。コメントの URL をレポートに書く
- 閉じるキーワード(close・fix・resolve の活用形)と `#番号` を続けて書かない。`/intervention` を書かない
