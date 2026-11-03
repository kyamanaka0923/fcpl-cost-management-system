工程 [B] 要件として、確定したナレッジから要件とシナリオを書き、画面の変更が要るかを判断してください。画面要件は書きません(screen-write の step が書きます)。コミット・プッシュ・PR の作成はしません。

**最初に:**
- タスクの本文(起動スクリプトが書いた Issue 番号・工程・ブランチ・既存の PR)と、Issue の本文・コメント(`gh issue view <n> --comments`)を読む。`git branch --show-current` が `req/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `gate0-approved` があることを確かめる。なければ作業せずに止める

**どの回かで作業を分ける:**
- 初回: Issue(要求・受け入れ条件の案)、`knowledge/` の `status: 確定` の業務ルールと用語、`docs/REQUIREMENTS.md` を読み、要件・`.feature` の `ルール:` とシナリオを書く
- 検証の差し戻しのあと(直前の step が requirements-review): Report Directory の `requirements-review.md` の指摘のうち、対象が「要件」のものを1件ずつ直す。直さない指摘は理由をレポートに書く。対象が「画面」の指摘だけなら、要件は変えずに「画面の変更が要る」を選ぶ
- 上流のやり直し(タスクの「上流のやり直し」が「あり」。人が Issue に `redo:req` を付けた): 下流の工程が止めて出した問いと、人の回答(`gh issue view <n> --comments` の最後の問いと、そのあとの「## 回答」)を読み、回答で決まったことだけを、この工程の成果物に反映する。それ以外は変えない。いまの列のラベルは下流の工程のもの(`stage:実装中` など)のままでよい
- 続き(タスクの「既存の PR」が番号)の初回: `gh pr view <番号> --comments` と `gh pr checks <番号>` で、PR のレビューの指摘と CI の失敗を読み、要件・`.feature` が原因のものを1件ずつ直す。CI の失敗は `gh run view <run-id> --log-failed` で原因を見る。この工程の変更と関係のない失敗・対応しない指摘は、変えずにその判断と根拠をレポートに書く。画面要件が原因なら、ここでは直さずに「画面の変更が要る」を選ぶ
- 続き(タスクの「既存の PR」が「なし」で、「前の実行のコミット」が 1 件以上)の初回: 人の回答待ちで止めたときの書きかけから再開する。Issue の問いと、そのあとの人の回答(`gh issue view <n> --comments` の「## 回答」)を読み、書きかけ(`git log --no-merges origin/main..HEAD` と `git diff "$(git merge-base origin/main HEAD)"`)を回答に合わせて直してから、残りを書き上げる。起動スクリプトが取り込んだ origin/main の変更(回答を受けて直した上流など)も読む。回答で決まらないことが残れば、もう一度要件の穴として止める

**書くとき:**
- 要件ファイルの形式と置き場は `requirements/README.md` に従う(書く前に読む)
- 変えてよいのは `requirements/`(`requirements/screens/` を除く)、`.feature`、`docs/REQUIREMENTS.md`、`knowledge/open-questions.md` だけ
- 要件文・`business_rules`・シナリオ・`@未実装` の決まりは persona の決まりに従う
- Issue の受け入れ条件案と、要件・シナリオの対応表をレポートに書く

**要るナレッジが未確定・ないとき:**
- 要件を書かずに止め、「要件の土台のナレッジが未確定・足りない」を選ぶ。ファイルは変えない(`knowledge/open-questions.md` に同じ問いがあれば重ねて書かない)
- レポートの「人への問い」に、要る業務ルールの ID と status、決まっていない問い(`OQ-…`)を書く。Issue へのラベルとコメントは escalate の step がする

**画面の変更が要るかの判断:**
- 次のどれかに当たれば「要る」
  - 書いた要件が、画面に出る項目・表示の条件・操作を新しく決める、または変える
  - 新しい画面が要る
  - 直前の `requirements-review.md` に対象が「画面」の指摘が残っている
  - 続きで、PR の指摘か CI の失敗の原因が画面要件(`requirements/screens/`)にある
- どれにも当たらなければ「要らない」。判断の根拠(要件の ID と、関係する画面 ID か「該当なし」)をレポートに書く
