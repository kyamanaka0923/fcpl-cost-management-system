本ライン([C] 仕様・[D] 実装)の書く step が要件の穴を見つけて止めました。Issue に問いを出して、この実行を止めてください。成果物は直さず、コミット・プッシュ・PR の作成もしません。

**手順:**

1. Report Directory の `triage.md`(Issue 番号・工程)と、書く step のレポート(`spec-write.md` か `implement.md`)の「要件の穴」を読む
2. ラベルを変える。`gh issue view <n> --json labels` でいまの列のラベル(`stage:…`)を読み、次を1つのコマンドで、他のコマンドとつながずに実行する
   - `gh issue edit <n> --remove-label <いまの列のラベル> --add-label stage:判断待ち --add-label needs-answer`
   - 列のラベルは1つだけにする(いまの列を外す)。外した列は、次のコメントに書く
   - ゲートラベルは触らない
3. 問いのコメントを `$TMPDIR` のファイルに日本語で書き、`gh issue comment <n> --body-file <ファイル>` で出す
   - どの工程で止めたか([C] 仕様 / [D] 実装)、ブランチ名、外した列のラベル(回答のあとに戻す列)
   - 穴の場所(要件・業務ルール・仕様の ID、ファイル)と、なぜ一通りに決まらないか
   - 問い: Yes/No か選択肢で答えられる形。各答えで何が起きるかも書く
   - 人が回答したあと、問いを出した工程からやり直すこと
   - 閉じるキーワードと `#番号` を続けて書かない。`/intervention` を書かない
4. `gh issue view <n> --json labels` で、`needs-answer` と `stage:判断待ち` が付き、列のラベルが `stage:判断待ち` だけになったことを確かめる
5. 作業ツリーに残った書きかけの変更(`git status --porcelain`)はそのままにし、プッシュしない。残っているファイルを結果に書く
