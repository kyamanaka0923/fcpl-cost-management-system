Report Directory の `triage.md` を読み、決まった工程の着手の準備をしてください。成果物(ナレッジ・要件・画面・仕様・コード)はこの step では書きません。

**手順:**

1. `triage.md` の Issue 番号・工程・(続きなら)既存の PR とブランチを確かめる。`gh issue view <n> --json state,labels` で、判定のときから列とゲートラベルが変わっていないことを確かめる。変わっていたら準備せずに止める
2. 作業ツリーがきれいか確かめる(`git status --porcelain` が空)。きれいでなければ準備せずに止める(他人の変更を混ぜない)
3. `git fetch origin` のあと、ブランチを用意する。切り替える前のブランチ名(`git branch --show-current`)をレポートに書く(止めたときに戻すため)
   - [A][B][C][D] の着手: 工程の種類のブランチを `origin/main` から切る(`git switch -c <種類>/<n>-<名前> origin/main`)。`<名前>` は Issue のタイトルから英小文字・数字・ハイフンで短く作る
   - [D] の続き(`stage:実装中`): 既存の `impl/<n>-…` ブランチに切り替え、`origin` の同じブランチにそろえる(`git switch <ブランチ>`、`git merge --ff-only origin/<ブランチ>`)。早送りできなければ止める
4. 前提ファイルを作る([C] と [D] だけ)
   - [C]: Issue が指す要件(`requirements/` の FR・`.feature` のシナリオ)と、その要件が参照する業務ルールを、origin/main の中から特定する。直前の [B] の PR(マージ済み。`gh pr list --state merged --search "<n> in:body"`)の変更ファイルを手がかりにしてよい。`bash tools/factory/premises.sh <n> spec <ファイル>…`
   - [D] の着手: [C] の前提(`.factory/premises/ISSUE-<n>.yml` が main にあればその一覧)に、直前の [C] の PR の処理ルール・`docs/api/openapi.json` を足して、`bash tools/factory/premises.sh <n> impl <ファイル>…`
   - [D] の続き: 既存の `.factory/premises/ISSUE-<n>.yml` をそのまま使う。ないときだけ [D] の着手と同じ手順で作る
   - スクリプトが「main にないファイル」で失敗したら、そのファイルを外さずに止める(承認されていない前提で進めない)
   - 前提にできるファイルが1つも特定できないときは止める
   - [A][B] では作らない
5. ここまでがすべてできたら、列のラベルを付け替える(知識の「列ラベルの付け替え」の表のとおり)
   - [D] の着手(`stage:仕様確定` から)だけ: `gh issue edit <n> --remove-label stage:仕様確定 --add-label stage:実装中` を、1つのコマンドで、他のコマンドとつながずに実行する。そのあと `gh issue view <n> --json labels` で、列のラベルが `stage:実装中` だけになったことを確かめる
   - [A][B][C] と [D] の続きは、ラベルを変えない

**止めるとき:**
- それまでに作ったブランチ・付け替えたラベルを、レポートに書く。消したり戻したりはしない
