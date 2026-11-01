工程 [B] 画面として、要件から画面要件と fixtures を書いてください。要件・`.feature` は書き換えません。コミット・プッシュ・PR の作成はしません。

**最初に:**
- Report Directory の `triage.md` と `requirements-write.md`(画面の変更が要る理由)を読む。`git branch --show-current` が `req/<n>-…` であることを確かめる。違えば作業せずに止める

**どの回かで作業を分ける:**
- requirements-write のあと: `requirements-write.md` に書かれた、画面の変更が要る理由に沿って画面要件を書く。直前に `requirements-review.md` の画面への指摘があれば、それも直す
- 検証の差し戻しのあと(直前の step が requirements-rework): Report Directory の `requirements-review.md` の指摘のうち、対象が「画面」のものを1件ずつ直す。直さない指摘は理由をレポートに書く

**書くとき:**
- 変えてよいのは `requirements/screens/`(`approved/` を除く)だけ
- `screens.yaml` の項目、fixtures の形、状態ごとの表示の条件、既存の画面に合わせる決まりは persona の決まりに従う
- 新しい状態の承認画像は撮らない。人に撮ってほしい状態(画面 ID と状態)をレポートの「問い」に書く
- 画面の差分の検査(`bash tools/factory/screens-docker.sh`)を動かせたら、その結果をレポートに書く。動かせなかったら「未確認」と書く
