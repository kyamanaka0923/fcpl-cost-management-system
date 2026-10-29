---
id: SPEC-003
title: 保存されている金額に 1 円未満の端数がないことの数え方
status: 有効
requirements: []
business_rules: [BR-BUD-007]
api: []
issues: [46]
---

## 要件と満たし方

- 要件はない。BR-BUD-007 の OQ-005 の回答「すでに保存されている金額は移行しない。端数つきの金額が保存されていないことを**数えて確かめる**。
  見つかったときの扱いは、そのときに決める」を、**どう数えるか**(対象の表と列・判定・確かめ方)に落としたもの
- 数えるのは実装の工程([D])。[SPEC-001](SPEC-001.md) の判定を入れる前の状態を数える

## ルール

### 対象の表と列

金額を保存している列は、いまのスキーマ(`DatabaseInitializer`)では次の 3 つだけ。ほかの表に金額の列はない。

| # | 表 | 列 | 何の金額 | 関係する要件 |
|---|---|---|---|---|
| 1 | `department_budget_lines` | `amount` | 明細の半期一括の金額(月次の明細では月別の合計) | FR-4.8、FR-4.10 |
| 2 | `department_budget_line_months` | `amount` | 明細の月別金額(月インデックス 1..6) | FR-4.11、FR-4.12 |
| 3 | `actual_entries` | `amount` | 実績の計上金額 | FR-6.7、FR-6.8 |

### 数え方

```
端数つきの行 ⇔ CAST(amount AS REAL) <> CAST(CAST(amount AS REAL) AS INTEGER)
```

```sql
SELECT
  (SELECT COUNT(*) FROM department_budget_lines
     WHERE CAST(amount AS REAL) <> CAST(CAST(amount AS REAL) AS INTEGER))        AS 明細の半期一括,
  (SELECT COUNT(*) FROM department_budget_line_months
     WHERE CAST(amount AS REAL) <> CAST(CAST(amount AS REAL) AS INTEGER))        AS 明細の月別,
  (SELECT COUNT(*) FROM actual_entries
     WHERE CAST(amount AS REAL) <> CAST(CAST(amount AS REAL) AS INTEGER))        AS 実績の計上;
```

- 列は `TEXT`(`decimal` を不変文化の文字列にしたもの)。端数のない金額も「100.0」のように小数点つきで入ることがあるため、
  **文字に「.」があるかでは数えない**。数値にしてから、0 方向へ切り捨てた値と等しいかで数える
- `CAST(... AS INTEGER)` は 0 方向への切り捨てなので、負の値(例 -0.5)も端数つきとして数える
- 数えるのは 1 円未満の端数だけ(0 未満かどうかは、この数え方では分けない)

### 確かめ方

1. 対象は、動いている環境の SQLite のファイル(接続文字列 `ConnectionStrings:Default`。既定は `costmanagement.db`)。環境ごとに 1 ファイル
2. 実装の工程で、対象のファイルに上の SQL を実行し、**3 つの件数と、実行した日時(JST)・対象のファイル**を #46 の PR に書く
3. 期待する結果は 3 つとも 0 件
4. 0 件でない列が 1 つでもあったときは、**作業を止めて** Issue #46 に `needs-answer` を付け、件数と値の例を書く
   (扱いは「そのときに決める」= BR-BUD-007、OQ-005 の回答。移行や丸めを仕様なしで行わない)

## 具体例

数え方(上の判定)が、どの行を端数つきとして数えるか。

| # | `amount` の中身 | 数えるか | 境界の理由 |
|---|---|---|---|
| 1 | `0` | 数えない | 下限ちょうど |
| 2 | `100` | 数えない | 端数なし |
| 3 | `100.0` | 数えない | 小数点つきの書き方でも端数なし |
| 4 | `100.00` | 数えない | 小数点以下が 2 桁でも端数なし |
| 5 | `100.5` | 数える | 1 円未満の端数 |
| 6 | `0.1` | 数える | 0 のすぐ上の端数 |
| 7 | `2000000.01` | 数える | 大きい値の端数 |
| 8 | `-0.5` | 数える | 負で端数もある値 |
| 9 | `-1` | 数えない | 負だが端数なし(この数え方は端数だけを数える) |

## テストの観点

| 観点 | テストレベル | 置き場所 |
|---|---|---|
| 具体例の 1〜9 行(数え方の SQL が端数つきの行だけを数えること) | 結合(Infrastructure) | `tests/integration/backend/Infrastructure`(一時 DB の 3 つの表に具体例の値を入れて数える) |
| 対象の 3 つの表と列が、いまのスキーマの金額の列のすべてであること | 結合(Infrastructure) | 同上(`pragma_table_info` で `amount` 列を持つ表を数え、3 つであることを確かめる) |

- 保存されている件数そのもの(確かめ方の 2〜4)はテストではなく、実装の工程の 1 回の確認。結果は PR に書く
