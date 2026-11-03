---
id: SPEC-004
title: 予算への保存の楽観ロック(更新番号で衝突を見つけて拒否する)
status: 有効
requirements: [FR-4.15, FR-4.16, FR-4.17, FR-4.18, FR-4.19, FR-4.20, FR-5.7, FR-5.8]
business_rules: [BR-BUD-008, BR-BUD-002, BR-BUD-007, BR-BUD-006]
api:
  - GET /api/departments/{id}/budgets
  - POST /api/departments/{id}/budgets
  - GET /api/budgets/{budgetId}
  - PUT /api/budgets/{budgetId}/lines
  - DELETE /api/budgets/{budgetId}/lines
  - POST /api/budgets/{budgetId}/rename-period-detail
  - POST /api/budgets/{budgetId}/approve
issues: [45]
---

## 要件と満たし方

この仕様で「保存」は、BR-BUD-008 の 4 つの操作(明細の保存・明細の削除・明細名の変更・承認)を指す。
「画面が読んだ時点の更新番号」は、画面が予算を最後に読み込んだとき、または自分の保存の応答で受け取った `updateNumber`(BR-BUD-008 の「画面が読んだ時点」)。

| 要件 | 満たす API | 処理ルール(下の節) |
|---|---|---|
| FR-4.15 | `PUT /api/budgets/{budgetId}/lines`・`DELETE /api/budgets/{budgetId}/lines`・`POST /api/budgets/{budgetId}/rename-period-detail` の `updateNumber`(クエリ)と 409 | 「判定の順」の順 2。「衝突を確実に見つける」 |
| FR-4.16 | 上の 3 つと `POST /api/budgets/{budgetId}/approve` の応答、`GET` の応答の `updateNumber` | 「更新番号」 |
| FR-4.17 | 明細の 3 つの操作の 400 | 「判定の順」の順 1(明細の操作の文言) |
| FR-5.7 | `POST /api/budgets/{budgetId}/approve` の `updateNumber` と 409 | 「判定の順」の順 2。「承認の書き込みを 1 つにまとめる」 |
| FR-5.8 | `POST /api/budgets/{budgetId}/approve` の 400 | 「判定の順」の順 1(承認の文言) |
| FR-4.18 | 409 の応答のあとの `GET /api/budgets/{budgetId}`(SCR-002)・`GET /api/departments/{id}/budgets`(SCR-003) | 「画面の処理」の 3・4 |
| FR-4.19 | 409 の応答の `ErrorResponse.error` | 「画面の処理」の 3 |
| FR-4.20 | SCR-002 から送る 4 つの操作の順 | 「画面の処理」の 2 |

- 画面の見た目(トーストの位置・表示する項目)は画面要件(`requirements/screens/screens.yaml` の SCR-002・SCR-003)が正。ここでは書かない
- 対象外: 前の保存が拒否されたとき、その応答を待つ間に同じ画面でされた次の保存の扱い(FR-4.20 の補足。OQ-018 は未回答。
  Issue #45 の 2026-11-02 の回答で、この作業項目から外した)。この仕様は決めず、テストもしない

## ルール

### 更新番号

予算(`DepartmentBudget`)は更新番号を 1 つ持つ。型は 32 ビットの整数(API では `integer`・`int32`)。

| 出来事 | その予算の更新番号 | 根拠 |
|---|---|---|
| 当初予算・改定版のドラフトを起票する(`POST /api/departments/{id}/budgets`) | 1 で始める | 起票は 4 つの操作に入らない。値は仕様で決める(用語集: いくつ増えるかは決めていない) |
| 保存を受け付ける(4 つの操作のどれか) | 1 増やす(1 回の要求で 1 だけ) | FR-4.16、BR-BUD-008 の行 1 |
| 保存を拒否する(衝突・状態・ほかのどの拒否でも) | 変えない | FR-4.16 の補足、BR-BUD-008 |
| 改定版の承認を受け付け、同じ課 × 半期の旧承認版が失効する | 旧承認版の更新番号は変えない(承認した版は 1 増える) | 失効は旧承認版への保存(4 つの操作)ではない(FR-4.16) |
| 改定版を起票する | 引き継ぎ元の予算の更新番号は変えない(新しい版は 1 で始める) | 起票は 4 つの操作に入らない |

- 旧承認版の更新番号を変えなくても、旧承認版への保存は状態の拒否(順 1)が先に当たるので、どの保存の結果も変わらない
- 永続化: 予算の表(`department_budgets`)に更新番号の列(整数、NOT NULL)を足す。列を足すときにすでにある行は 1 にする
  (いま運用している DB はない。SPEC-003)。スキーマの変え方は知識「DB スキーマ変更の手順」に従う

### API の入出力

| API | 入力に足すもの | 出力に足すもの |
|---|---|---|
| `GET /api/budgets/{budgetId}`、`POST /api/departments/{id}/budgets` | なし | `BudgetDetailDto.updateNumber`(必須) |
| `GET /api/departments/{id}/budgets` | なし | 各 `BudgetSummaryDto.updateNumber`(必須) |
| `PUT /api/budgets/{budgetId}/lines`、`DELETE /api/budgets/{budgetId}/lines`、`POST /api/budgets/{budgetId}/rename-period-detail`、`POST /api/budgets/{budgetId}/approve` | クエリ `updateNumber`(必須、整数)= 画面が読んだ時点の更新番号 | 受け付けたら 200 と `BudgetDetailDto`(`updateNumber` は受け付けたあとの値)。衝突なら 409 と `ErrorResponse` |

- `updateNumber` は DTO(record)の最後の引数に足す(`BudgetDetailDto` は `lines` のあと、`BudgetSummaryDto` は `plannedProfit` のあと)
- クエリ `updateNumber` は、エンドポイントのハンドラの引数で、ほかのクエリの引数より後ろに置く(`DELETE` は `periodDetail` のあと。ほかの 3 つは `budgetId` のあと)
- 409 は 4 つの操作のエンドポイントだけに、応答の型 `ErrorResponse`・`application/json` として宣言する(`/api` のグループ全体には付けない)
- クエリ `updateNumber` がない・整数として読めない要求は、ハンドラに入る前に 400 で拒否される(ASP.NET Core の引数の束縛。いまの必須のクエリ `category` がないときと同じ。本文の形は決めない)。予算は変わらない

### 判定の順(BR-BUD-008 のデシジョンテーブルと「拒否が重なったときに返す拒否」)

4 つの操作は、予算を読んだあと、次の順に上から見て、最初に当たったものだけを返す。

| 順 | 条件 | 結果 | HTTP | 文言(`ErrorResponse.error`) |
|---|---|---|---|---|
| 0 | 予算がない | 拒否 | 404 | 予算が見つかりません: {budgetId}(いまの文言のまま) |
| 1 | 予算が Approved か Superseded | 拒否(状態の拒否。衝突に当たっても返さない) | 400 | 明細の保存・削除・明細名の変更: 承認済み・失効済みの予算は編集できません。改定版を作成してください。 / 承認: ドラフト状態の予算のみ承認できます。 |
| 2 | クエリ `updateNumber` ≠ 予算の更新番号 | 拒否(衝突の拒否) | 409 | ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。 |
| 3 | 状態と衝突以外の判定に外れる(区分・案件・費目の指定、金額(SPEC-001)、費目一括と明細名ありの併用、削除・改名する明細がない、明細名の重複、明細のない予算の承認 など) | 拒否 | いまのまま(400 か 404) | いまの文言のまま |
| 4 | どれにも当たらない | 受け付ける。更新番号を 1 増やす | 200 | — |

- 順 2 は「違う」かどうかだけを見る。小さくても大きくても、負の数でも、違えば衝突(BR-BUD-008: 更新番号が違えば「ほかの保存が受け付けられた」とする)
- 順 1・2 は、順 3 のどの判定よりも先に行う。いまの明細の保存は、区分の読み取り・案件や費目の存在の確かめを状態の判定より先にしているので、
  その順を変える(BR-BUD-008 の判定の順。順 1 → 順 3 は「拒否が重なったときに返す拒否」の表どおり)
- 順 1 と順 2 はドメイン層で判定する(状態の判定はいまも `DepartmentBudget` にある)。順 2 で投げる例外は、`DomainException`(→ 400)と区別できる型にし、
  WebApi が 409 と `ErrorResponse` に変える
- どの拒否でも、DB の内容(状態・明細・更新番号、旧承認版の状態)は変えない

### 衝突を確実に見つける(読んでから書くまでの間の保存)

順 2 は予算を読んだときの値で比べるので、2 つの要求が同じ更新番号を読んで、どちらも順 2 を通ることがありうる
(サーバーレス構成の予約同時実行 = 1 はこれを起こさないが、それ以外の構成では起こる。BR-BUD-008 の「いまの実装」)。
そのため、書き込みでも確かめる。

- 予算の表の更新は、予算の ID と「読んだときの更新番号」の両方が一致する行だけを書き換え、更新番号を 1 増やす。
  書き換えた行が 0 件なら、その要求のどの書き込みもしない(トランザクションを戻す)で、順 2 と同じ 409 と文言で拒否する
- 明細の洗い替え(削除→挿入)は、上の予算の表の更新と同じトランザクションで行う(いまの「ヘッダ UPDATE + 明細 DELETE→INSERT」のまま)

### 承認の書き込みを 1 つにまとめる

- 承認では、承認する予算の更新(上の条件つき)と、同じ課 × 半期の旧承認版の失効を、1 つのトランザクションで書く
- 承認する予算の条件つきの更新が 0 件なら、旧承認版の失効も書かない(FR-5.7 の補足: 衝突で拒否したとき旧承認版は承認済みのまま)

### 画面の処理(SCR-002 予算編集・SCR-003 課詳細)

1. 画面は、予算を読んだ応答(`GET /api/budgets/{budgetId}`、`GET /api/departments/{id}/budgets` の各行)と、自分の保存を受け付けた応答の `updateNumber` を持ち、
   次の保存のクエリ `updateNumber` に送る。持つ値は、最後に受け取った応答の値に置き換える
2. SCR-002 は、4 つの操作を 1 つずつ送る。前の保存の応答を待つ間に次の保存の操作がされたら、前の保存を受け付けた応答を受け取ってから、
   その応答の `updateNumber` を付けて次の保存を送る(FR-4.20。BR-BUD-008 の例 4)。待っている保存が 2 つ以上なら、操作された順に送る
3. 保存の応答が 409 のとき、画面は応答の `ErrorResponse.error`(衝突の文言)をトーストに出し(FR-4.19)、予算を読み込み直す(FR-4.18)。
   SCR-002 は `GET /api/budgets/{budgetId}` で予算全体の表示と持つ `updateNumber` を置き換え、拒否された入力は入力欄から消える
4. SCR-003 で承認の応答が 409 のときは、`GET /api/departments/{id}/budgets` で予算バージョンの表と各行の `updateNumber` を置き換える
5. 409 で見分けるのは衝突だけ。400・404 の拒否の画面の動き(いまのトースト)は変えない(FR-4.18 の補足: 衝突以外の拒否は対象外)
6. 前の保存が拒否されたとき(409 でも 400 でも)の、待っている次の保存の扱いは決めない(上の「対象外」)

- 画面要件の fixtures(`requirements/screens/fixtures/SCR-002/通常.json`・`空.json`、`SCR-003/通常.json`)の応答は、`updateNumber` を持つ
  (Issue #45 の問いへの回答 A により [B] で足した。PR #96)。検査「画面要件と fixtures」(`node tools/factory/cli.mjs screens`)は、
  fixtures をこの仕様の差分を入れた OpenAPI のスキーマで確かめる

## 具体例

予算 P は課「開発1課」の 2026-H1 の Draft。明細は「売上高 × PJ-A = 1,000,000」「売上高 × PJ-B = 500,000」「期間費用 × LICENSE × AWS = 300,000」。
P の更新番号を n とする(n は起票と明細の登録で決まる値。行 1 だけは起票の直後)。「送る番号」はクエリ `updateNumber`。

| # | 入力 | 期待する結果 | 境界の理由 |
|---|---|---|---|
| 1 | 当初予算のドラフトを起票する | 201。`updateNumber` = 1 | 初期値 |
| 2 | 改定版を起票する(v1 は承認済み、更新番号 m) | 201。v2 の `updateNumber` = 1。v1 の更新番号は m のまま | 起票は保存ではない |
| 3 | 明細の保存(PJ-A = 1,200,000)、送る番号 n | 200。応答の `updateNumber` = n + 1。PJ-A = 1,200,000 | 一致(行 1) |
| 4 | 明細の保存(PJ-A = 0)、送る番号 n | 200。`updateNumber` = n + 1 | 金額の下限でも増える(FR-4.16 のシナリオ) |
| 5 | 明細の削除(PJ-B)、送る番号 n | 200。`updateNumber` = n + 1。PJ-B はない | 削除も増える |
| 6 | 明細名の変更(AWS → AWS本番)、送る番号 n | 200。`updateNumber` = n + 1 | 改名も増える |
| 7 | 承認、送る番号 n | 200。Approved。`updateNumber` = n + 1 | 承認も増える |
| 8 | 行 3 のあとに、明細の保存(PJ-A = 900,000)、送る番号 n | 409「ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。」。PJ-A = 1,200,000、更新番号 n + 1 のまま | 1 つ小さい(同じ明細。例 1) |
| 9 | 行 3 のあとに、明細の保存(PJ-B = 600,000)、送る番号 n | 409(行 8 と同じ文言)。PJ-B = 500,000 のまま | 別の明細でも衝突(例 2) |
| 10 | 明細の保存(PJ-A = 900,000)、送る番号 n + 1(予算は n) | 409。明細・更新番号は変わらない | 1 つ大きい |
| 11 | 明細の保存(PJ-A = 900,000)、送る番号 -1 | 409。明細・更新番号は変わらない | 負の数 |
| 12 | 行 3 のあとに、明細の削除(PJ-B)、送る番号 n | 409。PJ-B = 500,000 は残る | 削除の衝突 |
| 13 | 行 3 のあとに、明細名の変更(AWS → AWS本番)、送る番号 n | 409。明細名は AWS のまま | 改名の衝突 |
| 14 | 行 5 のあとに、明細の保存(PJ-A = 900,000)、送る番号 n | 409。PJ-A = 1,000,000、PJ-B はない | 削除のあとの衝突 |
| 15 | 行 6 のあとに、明細の保存(PJ-A = 900,000)、送る番号 n | 409。明細名は AWS本番 | 改名のあとの衝突 |
| 16 | 行 3 のあとに、承認、送る番号 n | 409。P は Draft のまま。更新番号 n + 1 のまま | 承認の衝突(例 7) |
| 17 | PJ-A・PJ-B・AWS をすべて削除したあと(更新番号 n + 3)、承認、送る番号 n | 409(「明細のない予算は承認できません。」ではない) | 衝突とほかの拒否の重なり(例 9) |
| 18 | 行 3 のあとに、明細の保存(PJ-B = -1)、送る番号 n | 409(「金額は0以上で入力してください。」ではない)。PJ-B = 500,000 のまま | 衝突と金額の拒否の重なり(例 10) |
| 19 | 行 3 のあとに、費目一括の LICENSE = 100 の保存(LICENSE には明細名ありの明細 AWS がある)、送る番号 n | 409(「この費目には明細があります。…」ではない) | 衝突と BR-BUD-006 の併用の拒否の重なり(FR-4.15 の補足) |
| 20 | 行 3 のあとに、存在しない案件 ID の明細の保存、送る番号 n | 409(404「案件が見つかりません: …」ではない) | 衝突と 404 のほかの拒否の重なり |
| 21 | 明細の保存(PJ-B = -1)、送る番号 n | 400「金額は0以上で入力してください。」。更新番号 n のまま | 一致していてほかの拒否(SPEC-001) |
| 22 | 行 7 のあとに、明細の保存(PJ-A = 900,000)、送る番号 n + 1 | 400「承認済み・失効済みの予算は編集できません。改定版を作成してください。」 | 状態の拒否・一致(行 3) |
| 23 | 行 7 のあとに、明細の保存・明細の削除・明細名の変更、送る番号 n | どれも 400(行 22 と同じ文言)。明細と更新番号 n + 1 は変わらない | 状態の拒否・衝突にも当たる(行 4。例 5) |
| 24 | 行 7 のあとに、承認、送る番号 n | 400「ドラフト状態の予算のみ承認できます。」。更新番号 n + 1 のまま | 承認の状態の拒否・衝突にも当たる(例 8) |
| 25 | 行 7 のあとに、区分 `Foo` の明細の保存、送る番号 n | 400「承認済み・失効済みの予算は編集できません。改定版を作成してください。」(「予算区分が不正です: Foo」ではない) | 状態の拒否とほかの拒否の重なり(順 1 → 順 3) |
| 26 | P を承認(v1)→ 改定版 v2 を起票・承認したあと、v1 へ明細の保存と承認、送る番号は v1 を承認の前に読んだ値 | 明細の保存は 400(行 22 の文言)、承認は 400(行 24 の文言)。v1 は Superseded のまま | 失効済み・衝突にも当たる |
| 27 | v1 承認済み(更新番号 m)、v2 Draft(更新番号 k)。v2 を承認、送る番号 k | 200。v2 は Approved・更新番号 k + 1。v1 は Superseded・更新番号 m のまま | 失効は保存ではない |
| 28 | 行 27 と同じ状態で、v2 に明細を保存(更新番号 k + 1)したあと、v2 を承認、送る番号 k | 409。v2 は Draft、v1 は Approved のまま(失効しない) | 改定版の承認の衝突(FR-5.7 の補足) |
| 29 | 2 つの要求がどちらも送る番号 n で順 2 を通り、続けて書き込む | 先に書いた方を受け付ける(更新番号 n + 1)。後の方は 409、DB の内容は先の書き込みのまま | 読んでから書くまでの間の保存 |
| 30 | 承認で行 29 と同じことが起き、承認する予算の条件つきの更新が 0 件になる | 409。旧承認版は Approved のまま | 承認の書き込みを 1 つにまとめる |
| 31 | クエリ `updateNumber` なしの明細の保存 | 400。予算は変わらない | 必須の入力がない |
| 32 | 存在しない予算 ID への明細の保存、送る番号 1 | 404「予算が見つかりません: {budgetId}」 | 順 0 |
| 33 | SCR-002: 予算を読み(`updateNumber` = n)、セルに金額を入れて離れる | `PUT …/lines?updateNumber=n` を送る。応答の n + 1 を持つ | 画面の処理 1 |
| 34 | SCR-002: PJ-A の保存の応答を待つ間に PJ-B のセルを離れる | PJ-B の `PUT` は、PJ-A の応答を受け取ったあとに `updateNumber` = (PJ-A の応答の値)で送る | 画面の処理 2(FR-4.20) |
| 35 | SCR-002: 同じセル(PJ-A)に 1,200,000、続けて 1 を応答を待たずに入れる | 2 つの `PUT` を入れた順に送る。PJ-A = 1 で終わる | 同じ明細の続けての保存(FR-4.20 のシナリオ) |
| 36 | SCR-002: 4 つの操作のどれかの応答が 409 | トーストに応答の `error` を出し、`GET /api/budgets/{budgetId}` を送って表示と持つ `updateNumber` を置き換える | 画面の処理 3 |
| 37 | SCR-003: 策定中の行の承認の応答が 409 | トーストに応答の `error` を出し、`GET /api/departments/{id}/budgets` を送って表と各行の `updateNumber` を置き換える。その行は策定中のまま | 画面の処理 4 |
| 38 | SCR-002: 保存の応答が 400(金額の拒否) | いまのとおりトーストに文言を出す。予算の読み込み直しは送らない | 画面の処理 5(衝突だけを見分ける) |
| 39 | クエリ `updateNumber=abc`(整数として読めない)の明細の保存 | 400。予算は変わらない | 整数でない入力 |

## テストの観点

| 観点 | テストレベル | 置き場所 |
|---|---|---|
| 行 1〜7・21〜24・27(更新番号の初期値と増え方、状態の拒否、判定の順の順 1 → 2 → 3)。拒否したときに状態・明細・更新番号が変わらないこと | 単体(Domain) | `tests/unit/backend/Domain`(`DepartmentBudget`) |
| 行 8〜19(衝突の拒否と、ドメイン層で判定するほかの拒否との重なり)。409 に変える例外の型と文言 | 単体(Domain) | 同上 |
| 行 20・25(Application 層の判定(案件の存在の確かめ・区分の読み取り)より先に、順 1・2 の拒否を返すこと)。拒否したときに明細・更新番号が変わらないこと | 結合(Application) | `tests/integration/backend/Application`(xUnit の例示テスト) |
| 受け付けた保存だけが更新番号を 1 増やし、拒否した保存は更新番号を変えない(不変条件) | 単体(Domain・プロパティ) | `tests/unit/backend/Domain/PropertyBased/DepartmentBudgetPropertyTests.cs`(モデルベースの操作の列に、送る番号を持たせる) |
| FR-4.15・FR-4.16・FR-4.17・FR-5.7・FR-5.8 のシナリオ(行 3〜9・12〜18・22〜24・26・28 を業務の流れで) | 結合(Gherkin) | `tests/integration/backend/Application/Features/課予算の策定と改定.feature` の `@FR-4.15` `@FR-4.16` `@FR-4.17` `@FR-5.7` `@FR-5.8`(`@未実装` を外す) |
| 行 2・27・28(改定版の起票と承認で、引き継ぎ元・旧承認版の更新番号と状態が変わらないこと) | 結合(Application) | `tests/integration/backend/Application`(xUnit の例示テスト) |
| 行 29・30(条件つきの更新が 0 件のとき 409 にし、どの書き込みもしないこと。承認では旧承認版も失効しないこと)。更新番号の列の往復。列を足すスキーマの作り直しの冪等性 | 結合(Infrastructure) | `tests/integration/backend/Infrastructure`(`RepositoryFixture`・`SchemaResetTests`) |
| 409 と `ErrorResponse.error` の文言(4 つの操作のそれぞれ)。行 3 の 200 の `updateNumber`。行 31・39 の 400。行 32 の 404 | API E2E | `tests/e2e/api`(xUnit の例示テスト `*Tests.cs`。`.feature` は要件の成果物なので足さない) |
| OpenAPI 定義が実装と一致すること(この仕様の差分を含む) | API E2E | `tests/e2e/api/OpenApiDocumentTests.cs` |
| 行 33〜38(送るクエリ `updateNumber` の値・送る順・409 のときのトーストと読み込み直し・400 のときに読み込み直さないこと) | 単体(フロントエンド) | `tests/unit/frontend/pages/`(SCR-002 は `support/fakeBudgetApi.ts` の `偽の予算API` に更新番号と 409 を足す。SCR-003 は MSW) |
| FR-4.18・FR-4.19・FR-4.20 のシナリオ(画面 → API → DB のつながり) | ブラウザ E2E | `tests/e2e/browser/features/予算明細の入力.feature` の `@FR-4.18` `@FR-4.19` `@FR-4.20`(`@未実装` を外す) |

- `.feature` のシナリオ(`@未実装` を含む)は要件の成果物なので、この仕様では変えない
- 4 つの操作を呼んでいるいまのテストのヘルパ(`UseCaseFixture` の日本語のヘルパ、`tests/e2e/api` の `APIコンテキスト`、`tests/e2e/browser` の `業務データ`)は、
  [D] でクエリ `updateNumber` を送るように直す(直前に読んだ値か、直前の応答の値を使う)
- 行 29・30 は、読んでから書くまでの間の保存を起こすため、同じ予算を 2 回読み込んだ 2 つの集約を、リポジトリで続けて書き込んで確かめる(並列の実行に頼らない)
- 行 34・35 の送る順は、`偽の予算API` の `fake.writes` の順と各要求の `updateNumber` で確かめる

## WebAPI 仕様(`docs/api/openapi.json`)

変える。[D] で実装から再生成したもの(`UPDATE_OPENAPI=1 dotnet test --filter OpenApi`)と一致させる。

| 場所 | 変更 |
|---|---|
| `PUT /api/budgets/{budgetId}/lines` | `parameters` の最後(`budgetId` のあと)にクエリ `updateNumber`(`required: true`、`integer`・`int32`)。`responses` の最後(`200` のあと)に `409`(`Conflict`、`ErrorResponse`) |
| `DELETE /api/budgets/{budgetId}/lines` | `parameters` の最後(`periodDetail` のあと)に同じクエリ。`responses` に同じ `409` |
| `POST /api/budgets/{budgetId}/rename-period-detail` | `budgetId` のあとに同じクエリ。`responses` に同じ `409` |
| `POST /api/budgets/{budgetId}/approve` | `budgetId` のあとに同じクエリ。`responses` に同じ `409` |
| `components.schemas.BudgetDetailDto` | `properties` の最後(`lines` のあと)に `updateNumber`(`integer`・`int32`)。`required` の最後に `updateNumber` |
| `components.schemas.BudgetSummaryDto` | `properties` の最後(`plannedProfit` のあと)に `updateNumber`(`integer`・`int32`)。`required` の最後に `updateNumber` |

- 操作の数は 28 のまま(`OpenAPI定義は全エンドポイントと応答の型を含む` の固定値は変わらない)
- `responses` の並び(`400`・`404`・`200`・`409`)は、`/api` のグループの宣言(400・404)→ 戻り値の型から推論される 200 → エンドポイントごとの宣言(409)の順になる、という見込みで書いた。
  この環境に .NET の SDK がなく、生成して確かめていない。[D] で再生成したものと並びが違えば、この仕様の差分の並びが誤り(中身は同じ)
