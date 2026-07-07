---
name: domain-design
description: ドメインモデルの変更・追加・設計判断を行うときに必ず読む。集約の追加、不変条件の変更、予算バージョン管理、売上と原価の対応付け、分析サービスの追加など、backend/src/CostManagement.Domain に触れる作業全般が対象。
model: claude-opus-4-8
---

# ドメイン設計スキル

**利用モデル**: 設計判断を伴うため `claude-opus-4-8` 以上を推奨(大規模なモデル変更は Fable 5)。
機械的なリネームや復元ファクトリの修正だけなら `claude-sonnet-5` で可。

## このシステムのドメインの決定事項(再検討しない)

- **明細は金額のみで管理する。数量×単価は使わない**(SE費用管理が主用途)。
  そのため価格差異・数量差異の分解は存在せず、差異 = 実績金額 − 予算金額 のみ
- **売上(RevenuePlan/ActualRevenue)と原価(CostPlan/ActualCost)は別集約**で、
  互いに ID 参照しない。対応付けは「品目名(文字列)」の緩い結合
  (原価側の `RevenueItem`、null = 共通費)。これにより売上予算と原価予算を独立に改定できる
- **予算のバージョン管理**: v1=当初、改定は明細コピーで新バージョン起票。
  Draft→Approved→Superseded。承認済みは編集不可。ドラフトは同時1件。
  明細なしは承認不可。承認時に旧承認版を Supersede するのは Application 層の責務
- 明細キー: 原価 = (費目, 売上対応品目, 年月) / 売上 = (品目, 年月)。同一キーは上書き(Upsert)
- 実績は同一キーに複数計上でき、分析時に合算する

## 戦術パターンの規約(アーキテクチャテストで強制される)

- 集約ルート・エンティティ・ドメインサービスは `sealed`
- 値オブジェクトは `readonly record struct`(Money, AccountingPeriod, 型付きID, CostElementCode)
- 集約に公開セッターを置かない。状態変更はドメインメソッド(`Approve()` 等)のみ
- リポジトリはポート(interface)としてドメイン層に定義。実装はインフラ層のみ
- ドメイン例外は `DomainException`(日本語メッセージ)。WebApi が 400 に変換する。
  未検出は Application 層の `NotFoundException` → 404
- 永続化からの復元は `Restore(...)` 静的ファクトリ(検証をスキップして状態を再構築)
- 分析はステートレスなドメインサービス + イミュータブルな record レポート。
  「品目別の合計 = 全体」のような整合性はテストで担保する

## 変更時の必須手順

1. ドメイン変更 → `CostManagement.Domain.Tests` を先に更新(日本語テスト名)
2. Restore ファクトリ・リポジトリ・DTO・AnalysisService への波及を必ず追う
3. スキーマが変わる場合は docs-update スキルと ci-and-env スキルの移行手順に従う
4. `docs/DESIGN.md` のクラス図・不変条件表を同期する
