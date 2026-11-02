工程 [B] 要件として、確定したナレッジから要件とシナリオを書き、画面の変更が要るかを判断してください。画面要件は書きません(screen-write の step が書きます)。コミット・プッシュ・PR の作成はしません。

**最初に:**
- Report Directory の `start.md` を読む。`git branch --show-current` が `req/<n>-…` であることを確かめる。違えば作業せずに止める
- `gh issue view <n> --json labels` で `gate0-approved` があることを確かめる。なければ作業せずに止める

**どの回かで作業を分ける:**
- 初回: Issue(要求・受け入れ条件の案)、`knowledge/` の `status: 確定` の業務ルールと用語、`docs/REQUIREMENTS.md` を読み、要件・`.feature` の `ルール:` とシナリオを書く
- 検証の差し戻しのあと(直前の step が requirements-review): Report Directory の `requirements-review.md` の指摘のうち、対象が「要件」のものを1件ずつ直す。直さない指摘は理由をレポートに書く。対象が「画面」の指摘だけなら、要件は変えずに「画面の変更が要る」を選ぶ
- CI の失敗のあと(直前の step が ci-failed): Report Directory の `ci-failure.md` を読み、要件・`.feature` が原因の失敗を直す。画面要件が原因なら、ここでは直さずに「画面の変更が要る」を選ぶ。この工程の変更と関係のない失敗なら、変えずにその判断と根拠をレポートに書く

**書くとき:**
- 要件ファイルの形式と置き場は `requirements/README.md` に従う(書く前に読む)
- 変えてよいのは `requirements/`(`requirements/screens/` を除く)、`.feature`、`docs/REQUIREMENTS.md`、`knowledge/open-questions.md` だけ
- 要件文・`business_rules`・シナリオ・`@未実装` の決まりは persona の決まりに従う
- Issue の受け入れ条件案と、要件・シナリオの対応表をレポートに書く

**要るナレッジが未確定・ないとき:**
- 要件を書かずに止め、「要件の土台のナレッジが未確定・足りない」を選ぶ。ファイルは変えない(`knowledge/open-questions.md` に同じ問いがあれば重ねて書かない)
- レポートの「人への問い」に、要る業務ルールの ID と status、決まっていない問い(`OQ-…`)を書く。Issue へのコメントとブランチの片付けは escalate の step がする

**画面の変更が要るかの判断:**
- 次のどれかに当たれば「要る」
  - 書いた要件が、画面に出る項目・表示の条件・操作を新しく決める、または変える
  - 新しい画面が要る
  - 直前の `requirements-review.md` に対象が「画面」の指摘が残っている
  - CI の失敗の原因が画面要件(`requirements/screens/`)にある
- どれにも当たらなければ「要らない」。判断の根拠(要件の ID と、関係する画面 ID か「該当なし」)をレポートに書く
