# 設計ドキュメント

総合原価管理システムのドメインモデル・ドメインサービス・アーキテクチャを図で示します。
図はすべて Mermaid で記述しており、GitHub 上でそのまま表示できます。

- [1. コンテキストマップ](#1-コンテキストマップ)
- [2. C4 モデル](#2-c4-モデル)
- [3. ドメインモデル クラス図](#3-ドメインモデル-クラス図)
- [4. ドメインサービス クラス図](#4-ドメインサービス-クラス図)

---

## 1. コンテキストマップ

本システムは単一の境界づけられたコンテキスト「**総合原価管理**」からなるモジュラーモノリスです。
コンテキスト内部は集約単位のモジュールに分かれ、共有カーネル(値オブジェクト)を介して連携します。

```mermaid
flowchart TB
    subgraph BC["境界づけられたコンテキスト: 総合原価管理 (Core Domain)"]
        direction TB
        SK["共有カーネル<br/>(Shared Kernel)<br/>Money / AccountingPeriod / DomainException"]

        PJ["プロジェクト<br/>(Projects)<br/>管理単位の定義"]
        CE["費目マスタ<br/>(CostElements)<br/>原価要素の分類"]
        PL["原価予算<br/>(Planning)<br/>バージョン管理・承認"]
        RV["売上予算・売上実績<br/>(Revenue)<br/>バージョン管理・承認"]
        AC["原価実績<br/>(Actuals)<br/>都度計上"]
        AN["分析<br/>(Analysis)<br/>差異分析・バージョン比較・損益"]

        PJ -->|管理単位を提供| PL
        PJ -->|管理単位を提供| RV
        PJ -->|管理単位を提供| AC
        CE -->|費目コードを参照| PL
        CE -->|費目コードを参照| AC
        RV -.->|"品目名で対応付け<br/>(売上対応品目)"| PL
        RV -.->|"品目名で対応付け"| AC

        PL -->|予算を入力| AN
        AC -->|実績を入力| AN
        RV -->|売上予実を入力| AN
    end

    SK --- PJ
    SK --- PL
    SK --- RV
    SK --- AC

    EXT1["会計システム(将来連携の候補・未実装)"]
    EXT2["勤怠・工数管理(将来連携の候補・未実装)"]
    EXT1 -.-> BC
    EXT2 -.-> BC
```

**設計上のポイント**

- 「売上」と「原価」は集約を分け、**品目名(文字列)による緩い対応付け**で結合しています。
  集約間を ID で強く参照しないことで、売上予算と原価予算を独立に改定できます
- 分析(Analysis)は状態を持たず、予算・実績の集約を入力として受け取る
  **ドメインサービス群**として実現しています(下流のコンフォーミスト的な位置づけ)

## 2. C4 モデル

### Level 1: システムコンテキスト図

```mermaid
C4Context
    title システムコンテキスト図
    Person(manager, "原価管理者", "予算の策定・承認、実績の計上、差異・損益の分析を行う")
    System(cms, "総合原価管理システム", "原価・売上高の予実管理と差異・損益分析")
    System_Ext(acct, "会計システム", "(将来連携の候補・未実装)")
    Rel(manager, cms, "利用する", "ブラウザ/HTTPS")
    Rel(cms, acct, "実績データ連携(将来)", "未実装")
```

### Level 2: コンテナ図

```mermaid
C4Container
    title コンテナ図
    Person(manager, "原価管理者")
    System_Boundary(cms, "総合原価管理システム") {
        Container(spa, "フロントエンド SPA", "React 19 + TypeScript + Vite", "予算編集・実績入力・差異/損益ダッシュボード")
        Container(api, "WebApi", "ASP.NET Core (.NET 10) Minimal API", "ユースケースの公開。ヘキサゴナルアーキテクチャの入力アダプタ")
        ContainerDb(db, "データベース", "SQLite", "プロジェクト/費目/予算(版管理)/実績")
    }
    Rel(manager, spa, "操作", "HTTPS")
    Rel(spa, api, "REST /api/*", "JSON/HTTP")
    Rel(api, db, "読み書き", "Dapper")
```

### Level 3: コンポーネント図(WebApi コンテナ内・ヘキサゴナルアーキテクチャ)

```mermaid
C4Component
    title コンポーネント図(WebApi)— ポート&アダプタ
    Container_Boundary(api, "WebApi (.NET 10)") {
        Component(endpoints, "Minimal API エンドポイント", "CostManagement.WebApi", "入力アダプタ。HTTP⇔DTO変換、例外→HTTPステータス変換")
        Component(app, "アプリケーションサービス", "CostManagement.Application", "ユースケース(入力ポート): Project/CostPlan/RevenuePlan/ActualCost/ActualRevenue/Analysis")
        Component(domain, "ドメイン", "CostManagement.Domain", "集約・値オブジェクト・ドメインサービス・リポジトリポート(中心。他層へ依存しない)")
        Component(infra, "Dapper リポジトリ", "CostManagement.Infrastructure", "出力アダプタ。リポジトリポートの実装、スキーマ初期化・移行")
    }
    ContainerDb(db, "SQLite", "", "")
    Rel(endpoints, app, "呼び出し")
    Rel(app, domain, "集約の操作・ドメインサービスの利用")
    Rel(infra, domain, "リポジトリポート(interface)を実装")
    Rel(app, infra, "ポート経由で永続化(DI で注入)")
    Rel(infra, db, "SQL", "Dapper")
```

依存の向きは常に内側(Domain)へ向かい、Domain は他のどの層にも依存しません。

## 3. ドメインモデル クラス図

### 3-1. 共有カーネル(値オブジェクト)

```mermaid
classDiagram
    class Money {
        <<Value Object>>
        +decimal Value
        +bool IsNegative
        +加算・減算・定数倍の演算子を提供()
    }
    class AccountingPeriod {
        <<Value Object>>
        +int Year
        +int Month
        +int Quarter
        +Parse(string) AccountingPeriod$
        +CompareTo(AccountingPeriod) int
    }
    class DomainException {
        <<Exception>>
        不変条件違反(HTTP 400 に変換)
    }
```

### 3-2. プロジェクト・費目マスタ

```mermaid
classDiagram
    class Project {
        <<Aggregate Root>>
        +ProjectId Id
        +string Code
        +string Name
        +int FiscalYear
        +ProjectStatus Status
        +Create(code, name, fiscalYear, now) Project$
        +Rename(name)
        +Complete()
    }
    class ProjectId {
        <<Value Object>>
        +Guid Value
    }
    class ProjectStatus {
        <<enumeration>>
        Active
        Completed
    }
    class CostElement {
        <<Aggregate Root>>
        +CostElementCode Code
        +string Name
        +CostElementType Type
        +Create(code, name, type) CostElement$
    }
    class CostElementCode {
        <<Value Object>>
        +string Value
    }
    class CostElementType {
        <<enumeration>>
        Material 材料費
        Labor 労務費
        Overhead 間接費
        Expense 経費
    }
    Project --> ProjectId
    Project --> ProjectStatus
    CostElement --> CostElementCode
    CostElement --> CostElementType
```

### 3-3. 予算・実績(中核の集約)

```mermaid
classDiagram
    class PlanStatus {
        <<enumeration>>
        Draft 策定中
        Approved 承認済
        Superseded 失効
    }

    class CostPlan {
        <<Aggregate Root>>
        +CostPlanId Id
        +ProjectId ProjectId
        +int Version
        +string Label
        +PlanStatus Status
        +DateTime? ApprovedAt
        +Money TotalAmount
        +CreateInitial(projectId, label, now) CostPlan$
        +ReviseFrom(basePlan, nextVersion, label, now) CostPlan$
        +UpsertLine(elementCode, revenueItem, period, amount)
        +RemoveLine(elementCode, revenueItem, period)
        +Approve(now)
        +Supersede()
    }
    class PlanLine {
        <<Entity>>
        +Guid Id
        +CostElementCode ElementCode
        +string? RevenueItem ※売上対応品目・null は共通費
        +AccountingPeriod Period
        +Money Amount
    }

    class RevenuePlan {
        <<Aggregate Root>>
        +RevenuePlanId Id
        +ProjectId ProjectId
        +int Version
        +string Label
        +PlanStatus Status
        +DateTime? ApprovedAt
        +Money TotalAmount
        +CreateInitial(projectId, label, now) RevenuePlan$
        +ReviseFrom(basePlan, nextVersion, label, now) RevenuePlan$
        +UpsertLine(itemName, period, amount)
        +RemoveLine(itemName, period)
        +Approve(now)
        +Supersede()
    }
    class RevenuePlanLine {
        <<Entity>>
        +Guid Id
        +string ItemName 品目(案件名)
        +AccountingPeriod Period
        +Money Amount
    }

    class ActualCost {
        <<Aggregate Root>>
        +ActualCostId Id
        +ProjectId ProjectId
        +CostElementCode ElementCode
        +string? RevenueItem ※売上対応品目・null は共通費
        +AccountingPeriod Period
        +Money Amount
        +string? Note
        +Record(...) ActualCost$
    }
    class ActualRevenue {
        <<Aggregate Root>>
        +ActualRevenueId Id
        +ProjectId ProjectId
        +string ItemName 品目
        +AccountingPeriod Period
        +Money Amount
        +string? Note
        +Record(...) ActualRevenue$
    }

    CostPlan "1" *-- "0..*" PlanLine : 明細(費目×品目×年月で一意)
    RevenuePlan "1" *-- "0..*" RevenuePlanLine : 明細(品目×年月で一意)
    CostPlan --> PlanStatus
    RevenuePlan --> PlanStatus
    CostPlan ..> Project : ProjectId で参照
    RevenuePlan ..> Project : ProjectId で参照
    ActualCost ..> Project : ProjectId で参照
    ActualRevenue ..> Project : ProjectId で参照
    PlanLine ..> CostElement : CostElementCode で参照
    ActualCost ..> CostElement : CostElementCode で参照
    PlanLine ..> RevenuePlanLine : 品目名で対応付け(緩い結合)
```

**不変条件(集約が強制するルール)**

| 集約 | 不変条件 |
|---|---|
| CostPlan / RevenuePlan | 承認済み・失効済みは編集不可(編集は Draft のみ) |
| 〃 | 明細のない予算は承認不可 |
| 〃 | 明細キー(費目 × 売上対応品目 × 年月/品目 × 年月)は集約内で一意(同一キーは上書き) |
| 〃 | 改定版のバージョン番号は基となる版より大きい |
| 〃 | 金額は0以上 |
| ActualCost / ActualRevenue | 金額は0以上。同一キーへの複数計上を許容(分析時に合算) |

### 3-4. リポジトリ(ポート)

実装は Infrastructure 層(Dapper + SQLite)。Domain 層にはインターフェースのみが属します。

```mermaid
classDiagram
    class IProjectRepository {
        <<interface>>
        +FindByIdAsync(ProjectId) Project?
        +FindByCodeAsync(string) Project?
        +ListAsync() IReadOnlyList~Project~
        +AddAsync(Project)
        +UpdateAsync(Project)
    }
    class ICostElementRepository {
        <<interface>>
        +FindByCodeAsync(CostElementCode) CostElement?
        +ListAsync() IReadOnlyList~CostElement~
        +AddAsync(CostElement)
    }
    class ICostPlanRepository {
        <<interface>>
        +FindByIdAsync(CostPlanId) CostPlan?
        +ListByProjectAsync(ProjectId) IReadOnlyList~CostPlan~
        +FindLatestApprovedAsync(ProjectId) CostPlan?
        +GetMaxVersionAsync(ProjectId) int
        +AddAsync(CostPlan)
        +UpdateAsync(CostPlan)
    }
    class IRevenuePlanRepository {
        <<interface>>
        +FindByIdAsync(RevenuePlanId) RevenuePlan?
        +ListByProjectAsync(ProjectId) IReadOnlyList~RevenuePlan~
        +FindLatestApprovedAsync(ProjectId) RevenuePlan?
        +GetMaxVersionAsync(ProjectId) int
        +AddAsync(RevenuePlan)
        +UpdateAsync(RevenuePlan)
    }
    class IActualCostRepository {
        <<interface>>
        +FindByIdAsync(ActualCostId) ActualCost?
        +ListByProjectAsync(ProjectId) IReadOnlyList~ActualCost~
        +AddAsync(ActualCost)
        +DeleteAsync(ActualCostId)
    }
    class IActualRevenueRepository {
        <<interface>>
        +FindByIdAsync(ActualRevenueId) ActualRevenue?
        +ListByProjectAsync(ProjectId) IReadOnlyList~ActualRevenue~
        +AddAsync(ActualRevenue)
        +DeleteAsync(ActualRevenueId)
    }
```

## 4. ドメインサービス クラス図

分析はすべて**状態を持たないドメインサービス**として実装し、集約(またはその分析結果)を
入力に取り、イミュータブルなレポート(record)を返します。

```mermaid
classDiagram
    class VarianceAnalysisService {
        <<Domain Service>>
        +Analyze(CostPlan, actuals, from?, to?) VarianceReport
    }
    class RevenueVarianceAnalysisService {
        <<Domain Service>>
        +Analyze(RevenuePlan, actuals, from?, to?) RevenueVarianceReport
    }
    class ProfitAnalysisService {
        <<Domain Service>>
        +Analyze(RevenueVarianceReport, VarianceReport) ProfitReport
    }
    class PlanComparisonService {
        <<Domain Service>>
        +Compare(basePlan, targetPlan) PlanComparisonReport
    }
    class RevenuePlanComparisonService {
        <<Domain Service>>
        +Compare(basePlan, targetPlan) RevenuePlanComparisonReport
    }

    class VarianceReport {
        <<record>>
        +List~VarianceLine~ Lines
        +decimal TotalPlannedAmount
        +decimal TotalActualAmount
        +decimal TotalVariance
    }
    class VarianceLine {
        <<record>>
        +string ElementCode
        +string? RevenueItem
        +AccountingPeriod Period
        +decimal PlannedAmount
        +decimal ActualAmount
        +decimal TotalVariance ※実績−予算
        +bool IsUnplanned ※予定外
        +bool IsAdverse ※正は予算超過で不利
    }
    class RevenueVarianceReport {
        <<record>>
        +List~RevenueVarianceLine~ Lines
        +decimal TotalVariance ほか合計
    }
    class RevenueVarianceLine {
        <<record>>
        +string ItemName
        +AccountingPeriod Period
        +decimal PlannedAmount
        +decimal ActualAmount
        +decimal TotalVariance
        +bool IsFavorable ※正は売上超過で有利
    }
    class ProfitReport {
        <<record>>
        +売上・原価・粗利の予実と差異
        +decimal? PlannedMarginRate
        +decimal? ActualMarginRate
        +List~ProfitItemLine~ ItemLines
        +List~ProfitPeriodLine~ PeriodLines
    }
    class ProfitItemLine {
        <<record>>
        +string? ItemName ※null は共通費
        +品目別の売上・原価・粗利の予実
    }
    class PlanComparisonReport {
        <<record>>
        +バージョン間の明細増減と合計
    }

    VarianceAnalysisService ..> VarianceReport : 生成
    VarianceReport *-- VarianceLine
    RevenueVarianceAnalysisService ..> RevenueVarianceReport : 生成
    RevenueVarianceReport *-- RevenueVarianceLine
    ProfitAnalysisService ..> ProfitReport : 生成
    ProfitReport *-- ProfitItemLine
    ProfitAnalysisService ..> VarianceReport : 入力
    ProfitAnalysisService ..> RevenueVarianceReport : 入力
    PlanComparisonService ..> PlanComparisonReport : 生成
    RevenuePlanComparisonService ..> PlanComparisonReport : 同型のレポートを生成
```

**分析の計算規則**

- 差異 = 実績金額 − 予算金額(符号付き)
  - 原価: 正 = 予算超過 = **不利差異**(`IsAdverse`)
  - 売上: 正 = 売上超過 = **有利差異**(`IsFavorable`)
- 突き合わせ粒度: 原価 = (費目, 売上対応品目, 年月)、売上 = (品目, 年月)。同一キーの実績は合算
- 損益: 売上品目と原価の売上対応品目を突き合わせて品目別粗利を算出。
  対応品目のない原価は「共通費」行に集計され、**品目別の合計は常に全体の損益と一致**する
