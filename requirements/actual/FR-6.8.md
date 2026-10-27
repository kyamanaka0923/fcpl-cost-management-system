---
id: FR-6.8
title: 実績の計上金額は 1 円単位の整数
priority: Must
status: 有効
business_rules: [BR-BUD-007, BR-ACT-001]
screens: []
issues: [46]
---

## 要件

もし実績の計上金額に 1 円未満の端数があるならば、システムは、その実績の計上を拒否する。

## 補足

- 判定と文言は予算の明細の金額と同じ(BR-BUD-007、FR-4.10)。半期一括の計上と月別の計上で判定は同じ(BR-ACT-001)
