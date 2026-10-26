---
id: BR-BUD-001
title: 予算は課 × 半期の単一集約で、4 区分をまとめて 1 回で承認する
status: 確定
sources: [SRC-REQ, SRC-ADR-0002, SRC-ADR-0004]
requirements: [FR-4.1, FR-4.2]
---

## ルール

- 予算(DepartmentBudget)は、課 × 半期ごとに作る。バージョンを重ねても、課 × 半期の単位は変わらない
- 1 つの予算は次の 4 区分の明細を持ち、**4 区分をまとめて 1 回で承認する**

| 区分 | 内訳の単位 |
|---|---|
| 売上高(Revenue) | 案件 |
| 加工費(Processing) | 案件 |
| 外注費(Outsourcing) | 案件 |
| 期間費用(PeriodCost) | 費目(さらに明細名で分けられる。BR-BUD-006) |

- 売上と原価を別の予算・別の承認に分けない(廃止済み。戻さない。ADR-0004)
