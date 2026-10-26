# 判断の記録

判断の記録(なぜそう決めたか)の正は [docs/adr/](../../docs/adr/README.md) に置く。ここには写さない(二重管理しない)。

業務ルールに効いている ADR:

| ADR | 決めたこと | 業務ルール |
|---|---|---|
| [0002](../../docs/adr/0002-department-half-single-aggregate.md) | 予算は課 × 半期の単一集約 | BR-BUD-001、BR-BUD-002 |
| [0003](../../docs/adr/0003-amount-only-lines.md) | 明細は金額のみ(数量 × 単価なし) | BR-BUD-007 |
| [0004](../../docs/adr/0004-four-categories-single-approval.md) | 4 区分を 1 回で承認 | BR-BUD-001 |
| [0005](../../docs/adr/0005-category-total-derived-from-lines.md) | 区分合計は明細合計 | BR-BUD-003、BR-BUD-004 |
| [0006](../../docs/adr/0006-division-department-two-levels.md) | 部 > 課の 2 階層、部は集計ビュー | BR-ORG-001、BR-DIV-001 |
| [0008](../../docs/adr/0008-project-guid-reference.md) | 案件は GUID 参照、コードは課ごとに一意 | BR-BUD-003 |
| [0009](../../docs/adr/0009-monthly-input-half-aggregation.md) | 月次入力を許し、集計は半期 | BR-BUD-005、BR-FH-001 |
| [0013](../../docs/adr/0013-period-cost-detail-lines.md) | 期間費用の明細名(費目一括と排他) | BR-BUD-006 |

業務の判断を新しく決めたとき(問いへの回答で業務ルールが変わったときなど)は、ADR を足してここに 1 行足す。
