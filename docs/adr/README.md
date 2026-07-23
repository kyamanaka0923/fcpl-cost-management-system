# アーキテクチャ決定記録(ADR)

本ディレクトリは、総合原価管理システムで下した**重要な設計判断**を記録する。
各 ADR は「なぜその決定に至ったか(背景)」「決定内容」「結果(トレードオフ)」を残し、
後から経緯を辿れるようにする。**決定を覆す場合は、該当 ADR を Superseded にして新しい ADR を追加**する
(既存 ADR を書き換えて歴史を消さない)。

形式は [Michael Nygard 形式](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)を用いる。

## 一覧

| ID | タイトル | 状態 |
|---|---|---|
| [0001](0001-hexagonal-ddd.md) | ヘキサゴナルアーキテクチャ + DDD 戦術パターンの採用 | Accepted |
| [0002](0002-department-half-single-aggregate.md) | 予算の管理単位を「課 × 半期」の単一集約にする | Accepted |
| [0003](0003-amount-only-lines.md) | 明細は金額のみとし数量 × 単価を採用しない | Accepted |
| [0004](0004-four-categories-single-approval.md) | 4区分を1予算としてまとめて承認する | Accepted |
| [0005](0005-category-total-derived-from-lines.md) | 課の区分合計は常に明細合計として導出する | Accepted |
| [0006](0006-division-department-two-levels.md) | 組織を部 > 課の2階層とし部は集計ビューにする | Accepted |
| [0007](0007-sqlite-dapper-no-legacy-migration.md) | 永続化は SQLite + Dapper、旧世代スキーマは移行せず作り直す | Accepted |
| [0008](0008-project-guid-reference.md) | 案件は GUID 参照・コードは課ごとに一意で編集可能にする | Accepted |
| [0009](0009-monthly-input-half-aggregation.md) | 明細ごとの月次入力を許容し集計は半期粒度のままにする | Accepted |
| [0010](0010-serverless-deployment.md) | クラウドはサーバーレス構成(Lambda + EFS + S3 + CloudFront)で提供する | Accepted |
| [0011](0011-testing-strategy-and-mutation.md) | テスト戦略(多層テスト + カバレッジ + ミューテーションテスト) | Accepted |
| [0012](0012-frontend-svg-and-toast.md) | フロントは自前 SVG 描画・エラーは固定トースト通知 | Accepted |
| [0013](0013-period-cost-detail-lines.md) | 期間費用は費目内を明細名で細分できる(費目一括との排他) | Accepted |

## 状態の凡例

- **Proposed**: 提案中
- **Accepted**: 採用・有効
- **Superseded by ADR-xxxx**: 新しい決定に置き換えられた
- **Deprecated**: 廃止(置換なし)
