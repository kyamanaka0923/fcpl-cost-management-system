# 仕様(specs/)

工程 [C] の成果物。承認済みの[要件](../requirements/README.md)を、**どう作るか**が一通りに決まるところまで具体にする
(PROC-DEV-001 3.2、[ADR-0015](../docs/adr/0015-software-factory.md))。
着手してよいのは、Issue が `stage:着手待ち`(`gate1-approved` 付き)のときだけ。
ゲート2 の承認は、仕様を変える PR(`spec/<n>-…` ブランチ)を人がマージし、Issue に `gate2-approved` を付けること。

## 仕様の 3 つ

| 仕様 | 正の置き場 | 中身 |
|---|---|---|
| WebAPI 仕様 | 変更は `api/ISSUE-<n>.openapi.json`。いまの API の正は [docs/api/openapi.json](../docs/api/openapi.json)(実装から作る) | パス・メソッド・リクエストとレスポンスのスキーマ・エラーの HTTP ステータス |
| 処理ルール | [rules/SPEC-xxx.md](rules/README.md) | 計算・判定・状態の変化。具体例の表付き |
| テスト計画 | [test-plan.md](test-plan.md) と、処理ルールの「テストの観点」 | テストレベルごとの母集団と完了基準 |

- API の識別は `operationId` ではなく **メソッドとパス**(`PUT /api/budgets/{budgetId}/lines`)で書く(いまの定義に `operationId` がないため)
- 処理ルールは、業務ルール([knowledge/business-rules/](../knowledge/business-rules/))をシステムの言葉に置き換えたもの。
  業務ルールにない決まりを足すときは、仕様に書かずに作業を止め、Issue に `needs-answer` を付けて問いを書く
- 画面の仕様は、画面要件([`requirements/screens/`](../requirements/screens/README.md))が正。ここには書かない

## 仕様を書くときの順番

1. Issue の要件(`requirements/<領域>/FR-x.y.md`)と、そこから参照される業務ルールを読む
2. 要件 1 件ごとに、どの API・どの処理ルールで満たすかを決める。満たし方が一通りに決まらなければ、問いにする
3. API を変えるなら `api/ISSUE-<n>.openapi.json` を書く(下の「API の変更の書き方」)。変えないなら作らない
4. 処理ルール `rules/SPEC-xxx.md` を書くか、既存のものを直す。具体例の表は、境界の値を必ず含める
5. 処理ルールの「テストの観点」に、どのテストレベルで何を確かめるかを書く。`.feature` のシナリオは要件([B])の成果物なので、
   ここでは変えない(`spec/` のブランチは `.feature` を触れない)。シナリオが足りないと分かったら、要件の穴として `needs-answer` にする
6. 前提にした要件・業務ルールを `.factory/premises/ISSUE-<n>.yml` に書く

## API の変更の書き方

仕様の PR(`spec/`)は `docs/api/openapi.json` を変えない。`docs/api/openapi.json` は実装から作るもので、
CI が実装と一致することを確かめている(実装より先に変えると、ゲート2 の PR の CI が落ちる。#98)。
API の変更は `api/ISSUE-<Issue番号>.openapi.json` に、**変える操作とスキーマだけ**を `docs/api/openapi.json` と同じ形で書く。

```json
{
  "paths": {
    "/api/budgets/{budgetId}/lines": {
      "put": { "parameters": [ … ], "requestBody": { … }, "responses": { "200": { … }, "409": { … } } }
    },
    "/api/old-endpoint": { "delete": null }
  },
  "components": {
    "schemas": {
      "BudgetDetailDto": { "type": "object", "required": [ … ], "properties": { … } },
      "OldDto": null
    }
  }
}
```

- 書けるのは `paths`(パス → メソッド → 操作)と `components.schemas` だけ
- 書いた操作・スキーマは**丸ごと**置き換わる(なければ足す)。項目を 1 つ足すときも、操作・スキーマの全体を書く。`null` は消す
- 書き方(`$ref`・`format`・応答の形)は、いまの `docs/api/openapi.json` に合わせる。[D] で実装から作り直したものと比べるため
- 検査(CI「仕様の API と OpenAPI(api-spec)」。手元では `node tools/factory/cli.mjs api-spec`)
  - `spec/<n>-…`: 形が正しいか、当てたあとの `$ref` が解決できるか。検査「画面要件と fixtures」も、この差分を当てた OpenAPI で fixtures を確かめる
  - `impl/<n>-…`: 実装から作り直した `docs/api/openapi.json` に、ここに書いたことがすべて入っているか(実装が足した説明・`format` などは許す)
- マージしたあとも消さない(どの Issue で API をどう変えたかの記録)。あとの Issue で同じ API を変えるときは、その Issue の新しいファイルに書く
