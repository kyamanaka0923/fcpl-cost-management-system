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
        SK["共有カーネル<br/>(Shared Kernel)<br/>Money / FiscalHalf / DomainException"]

        DV["部<br/>(Divisions)<br/>課の上位組織・集計単位"]
        DP["課<br/>(Departments)<br/>予算策定の管理単位・部に属する"]
        PJ["案件<br/>(Projects)<br/>課に属する内訳マスタ"]
        CE["費目マスタ<br/>(CostElements)<br/>期間費用の内訳"]
        BG["課予算<br/>(Budgeting)<br/>課×半期・4区分・バージョン管理・承認"]
        AC["実績<br/>(Actuals)<br/>都度計上"]
        AN["分析<br/>(Analysis)<br/>差異分析・バージョン比較・損益・部集計"]

        DV -->|課が所属| DP
        DP -->|管理単位を提供| BG
        DP -->|管理単位を提供| AC
        DP -->|案件が所属| PJ
        PJ -->|"案件IDを参照<br/>(売上高・加工費・外注費の明細)"| BG
        PJ -->|案件IDを参照| AC
        CE -->|"費目コードを参照<br/>(期間費用の明細)"| BG
        CE -->|費目コードを参照| AC

        BG -->|予算を入力| AN
        AC -->|実績を入力| AN
        AN -->|"配下課を合計"| DV
    end

    SK --- DV
    SK --- DP
    SK --- BG
    SK --- AC

    EXT1["会計システム(将来連携の候補・未実装)"]
    EXT2["勤怠・工数管理(将来連携の候補・未実装)"]
    EXT1 -.-> BC
    EXT2 -.-> BC
```

**設計上のポイント**

- 組織は**部(Division)> 課(Department)の2階層**。課は必ず1つの部に属します。
  部は予算を策定せず、配下課の予実を合計する集計ビュー(`DivisionBudgetSummaryService`)です
- 予算は**課 × 半期の単一集約**(DepartmentBudget)で、売上高・加工費・外注費・期間費用の
  4区分をまとめて1承認します(売上と原価を別集約で独立承認する方式は採っていません)
- 課の区分合計は**常に明細の合計として導出**します(ヘッダに金額を持たない=直接入力不可を
  構造的に保証)
- 案件・費目はマスタとして ID / コードで参照します(旧世代の「品目名の緩い結合」は廃止)。
  **案件(Project)は課に属し、案件コードは課ごとに一意**(別の課では同じコードを使える)。
  **費目(CostElement)はシステム全体で共通のマスタ**(課ごとの設定ではない)
- 分析(Analysis)は状態を持たず、予算・実績の集約を入力として受け取る
  **ドメインサービス群**として実現しています

## 2. C4 モデル

### Level 1: システムコンテキスト図

```mermaid
C4Context
    title システムコンテキスト図
    Person(manager, "原価管理者", "課の半期予算の策定・承認、実績の計上、差異・損益の分析を行う")
    System(cms, "総合原価管理システム", "課別半期予算の予実管理と差異・損益分析")
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
        ContainerDb(db, "データベース", "SQLite", "課/案件/費目/課予算(版管理)/実績")
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
        Component(app, "アプリケーションサービス", "CostManagement.Application", "ユースケース(入力ポート): Department/Project/CostElement/DepartmentBudget/ActualEntry/Analysis")
        Component(domain, "ドメイン", "CostManagement.Domain", "集約・値オブジェクト・ドメインサービス・リポジトリポート(中心。他層へ依存しない)")
        Component(infra, "Dapper リポジトリ", "CostManagement.Infrastructure", "出力アダプタ。リポジトリポートの実装、スキーマ初期化(旧世代テーブルの破棄・作り直し)")
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
    class FiscalHalf {
        <<Value Object>>
        +int Year
        +HalfTerm Half ※H1 上期・H2 下期
        +Parse(string) FiscalHalf$
        +CompareTo(FiscalHalf) int
    }
    class HalfTerm {
        <<enumeration>>
        H1 上期
        H2 下期
    }
    class DomainException {
        <<Exception>>
        不変条件違反(HTTP 400 に変換)
    }
    FiscalHalf --> HalfTerm
```

### 3-2. 課・案件・費目マスタ

```mermaid
classDiagram
    class Division {
        <<Aggregate Root>>
        +DivisionId Id
        +string Code
        +string Name
        +Create(code, name, now) Division$
        +Rename(name)
    }
    class DivisionId {
        <<Value Object>>
        +Guid Value
    }
    class DivisionBudgetApproval {
        <<Aggregate Root>>
        +DivisionBudgetApprovalId Id
        +DivisionId DivisionId
        +FiscalHalf FiscalHalf
        +DateTime ApprovedAt
        +Approve(divisionId, fiscalHalf, now) DivisionBudgetApproval$
        ※レコードの有無 = 承認状態
    }
    class Department {
        <<Aggregate Root>>
        +DepartmentId Id
        +DivisionId DivisionId ※所属する部
        +string Code
        +string Name
        +Create(divisionId, code, name, now) Department$
        +Rename(name)
    }
    class DepartmentId {
        <<Value Object>>
        +Guid Value
    }
    class Project {
        <<Aggregate Root>>
        +ProjectId Id ※GUID・他集約からの参照キー
        +DepartmentId DepartmentId ※所属する課
        +string Code ※課ごとに一意・編集可
        +string Name
        +Create(departmentId, code, name, now) Project$
        +Rename(name)
        +Edit(code, name) ※コード・名称を変更(参照はIdで保持)
    }
    class ProjectId {
        <<Value Object>>
        +Guid Value
    }
    class CostElement {
        <<Aggregate Root>>
        +CostElementCode Code ※システム全体で共通
        +string Name
        +Create(code, name) CostElement$
    }
    class CostElementCode {
        <<Value Object>>
        +string Value
    }
    Division --> DivisionId
    DivisionBudgetApproval ..> Division : DivisionId で参照
    Department --> DepartmentId
    Department ..> Division : DivisionId で参照
    Project --> ProjectId
    Project ..> Department : DepartmentId で参照
    CostElement --> CostElementCode
```

### 3-3. 課予算・実績(中核の集約)

```mermaid
classDiagram
    class BudgetStatus {
        <<enumeration>>
        Draft 策定中
        Approved 承認済
        Superseded 失効
    }
    class BudgetCategory {
        <<enumeration>>
        Revenue 売上高 ※案件別
        Processing 加工費 ※案件別
        Outsourcing 外注費 ※案件別
        PeriodCost 期間費用 ※費目別
    }

    class DepartmentBudget {
        <<Aggregate Root>>
        +DepartmentBudgetId Id
        +DepartmentId DepartmentId
        +FiscalHalf FiscalHalf
        +int Version
        +string Label
        +BudgetStatus Status
        +DateTime? ApprovedAt
        +CategoryTotal(category) Money ※常に明細合計
        +Money TotalCost ※加工費+外注費+期間費用
        +Money PlannedProfit ※売上高−総コスト
        +CreateInitial(departmentId, fiscalHalf, label, now) DepartmentBudget$
        +ReviseFrom(baseBudget, nextVersion, label, now) DepartmentBudget$
        +UpsertProjectLine(category, projectId, amount)
        +UpsertPeriodCostLine(elementCode, amount)
        +RemoveProjectLine(category, projectId)
        +RemovePeriodCostLine(elementCode)
        +Approve(now)
        +Supersede()
    }
    class BudgetLine {
        <<Entity>>
        +Guid Id
        +BudgetCategory Category
        +ProjectId? ProjectId ※案件系区分で必須
        +CostElementCode? ElementCode ※期間費用で必須
        +string? PeriodDetail ※費目内を細分する明細名。null=費目一括
        +Money Amount ※半期合計
        +IReadOnlyDictionary~int,Money~ MonthlyAmounts ※月次モードのみ
        +bool IsMonthly ※月別金額を持つ=月次
    }

    class ActualEntry {
        <<Aggregate Root>>
        +ActualEntryId Id
        +DepartmentId DepartmentId
        +FiscalHalf FiscalHalf
        +BudgetCategory Category
        +ProjectId? ProjectId ※案件系区分で必須
        +CostElementCode? ElementCode ※期間費用で必須
        +string? PeriodDetail ※期間費用の明細名。計画にない名は予定外
        +int? Month ※計上月(1..6)。半期一括は null
        +Money Amount
        +string? Note
        +Record(...) ActualEntry$
    }

    DepartmentBudget "1" *-- "0..*" BudgetLine : 明細(区分×案件 or 費目で一意)
    DepartmentBudget --> BudgetStatus
    BudgetLine --> BudgetCategory
    ActualEntry --> BudgetCategory
    DepartmentBudget ..> Department : DepartmentId で参照
    ActualEntry ..> Department : DepartmentId で参照
    BudgetLine ..> Project : ProjectId で参照
    BudgetLine ..> CostElement : CostElementCode で参照
    ActualEntry ..> Project : ProjectId で参照
    ActualEntry ..> CostElement : CostElementCode で参照
```

**不変条件(集約が強制するルール)**

| 集約 | 不変条件 |
|---|---|
| DepartmentBudget | 承認済み・失効済みは編集不可(編集は Draft のみ) |
| 〃 | 明細のない予算は承認不可 |
| 〃 | 明細キー(区分 × 案件/期間費用 × 費目)は集約内で一意(同一キーは上書き) |
| 〃 | 売上高・加工費・外注費の明細は案件必須(費目は指定不可)。期間費用の明細は費目必須(案件は指定不可) |
| 〃 | 期間費用は費目内を明細名でさらに細分できる(自由入力)。1費目内で「費目一括(明細名なし)」と「明細(明細名あり)」は併用不可(二重計上の防止) |
| 〃 | 改定版のバージョン番号は基となる版より大きい |
| 〃 | 金額は0以上 |
| 〃 | 区分合計はヘッダに持たず常に明細合計として導出(課レベルの直接入力は構造的に不可) |
| ActualEntry | 区分と案件/費目の排他は予算明細と同じ。金額は0以上。同一キーへの複数計上を許容(分析時に合算) |
| (Application 層) | ドラフトは同一(課, 半期)に1つまで。承認時に旧承認版を Supersede。案件は同一課所属のみ明細に使える |

### 3-4. リポジトリ(ポート)

実装は Infrastructure 層(Dapper + SQLite)。Domain 層にはインターフェースのみが属します。

```mermaid
classDiagram
    class IDivisionRepository {
        <<interface>>
        +FindByIdAsync(DivisionId) Division?
        +FindByCodeAsync(string) Division?
        +ListAsync() IReadOnlyList~Division~
        +AddAsync(Division)
        +UpdateAsync(Division)
    }
    class IDivisionBudgetApprovalRepository {
        <<interface>>
        +FindAsync(DivisionId, FiscalHalf) DivisionBudgetApproval?
        +AddAsync(DivisionBudgetApproval)
        +DeleteAsync(DivisionId, FiscalHalf)
    }
    class IDepartmentRepository {
        <<interface>>
        +FindByIdAsync(DepartmentId) Department?
        +FindByCodeAsync(string) Department?
        +ListByDivisionAsync(DivisionId) IReadOnlyList~Department~
        +AddAsync(Department)
        +UpdateAsync(Department)
    }
    class IProjectRepository {
        <<interface>>
        +FindByIdAsync(ProjectId) Project?
        +FindByCodeAsync(DepartmentId, string) Project?
        +ListByDepartmentAsync(DepartmentId) IReadOnlyList~Project~
        +AddAsync(Project)
        +UpdateAsync(Project)
    }
    class ICostElementRepository {
        <<interface>>
        +FindByCodeAsync(CostElementCode) CostElement?
        +ListAsync() IReadOnlyList~CostElement~
        +AddAsync(CostElement)
    }
    class IDepartmentBudgetRepository {
        <<interface>>
        +FindByIdAsync(DepartmentBudgetId) DepartmentBudget?
        +ListAsync(DepartmentId, FiscalHalf) IReadOnlyList~DepartmentBudget~
        +FindLatestApprovedAsync(DepartmentId, FiscalHalf) DepartmentBudget?
        +GetMaxVersionAsync(DepartmentId, FiscalHalf) int
        +AddAsync(DepartmentBudget)
        +UpdateAsync(DepartmentBudget)
    }
    class IActualEntryRepository {
        <<interface>>
        +FindByIdAsync(ActualEntryId) ActualEntry?
        +ListAsync(DepartmentId, FiscalHalf) IReadOnlyList~ActualEntry~
        +AddAsync(ActualEntry)
        +DeleteAsync(ActualEntryId)
    }
```

## 4. ドメインサービス クラス図

分析はすべて**状態を持たないドメインサービス**として実装し、集約(またはその分析結果)を
入力に取り、イミュータブルなレポート(record)を返します。

```mermaid
classDiagram
    class BudgetVarianceAnalysisService {
        <<Domain Service>>
        +Analyze(DepartmentBudget, actuals) VarianceReport
    }
    class BudgetComparisonService {
        <<Domain Service>>
        +Compare(baseBudget, targetBudget) BudgetComparisonReport
    }
    class ProfitAnalysisService {
        <<Domain Service>>
        +Analyze(VarianceReport) ProfitReport
    }
    class DivisionBudgetSummaryService {
        <<Domain Service>>
        +Summarize(課別VarianceReportの一覧) DivisionSummaryReport
    }

    class VarianceReport {
        <<record>>
        +List~CategoryVariance~ Categories
        +売上高とコストの予実・差異の合計
    }
    class CategoryVariance {
        <<record>>
        +BudgetCategory Category
        +List~VarianceLine~ Lines
        +区分サブトータル(予算・実績・差異)
    }
    class VarianceLine {
        <<record>>
        +BudgetCategory Category
        +Guid? ProjectId ※案件系区分
        +string? ElementCode ※期間費用
        +decimal PlannedAmount
        +decimal ActualAmount
        +decimal Variance ※実績−予算
        +bool IsUnplanned ※予定外
        +bool IsFavorable ※売上は正が有利・コストは負が有利
    }
    class BudgetComparisonReport {
        <<record>>
        +バージョン間の区分別・明細別の増減
    }
    class ProfitReport {
        <<record>>
        +課全体の売上高・総コスト・損益の予実と差異
        +期間費用の予実 ※課共通
        +decimal? PlannedMarginRate
        +decimal? ActualMarginRate
        +List~ProjectProfitLine~ ProjectLines
    }
    class ProjectProfitLine {
        <<record>>
        +Guid ProjectId
        +案件別の売上高・加工費・外注費・損益の予実
    }

    BudgetVarianceAnalysisService ..> VarianceReport : 生成
    VarianceReport *-- CategoryVariance
    CategoryVariance *-- VarianceLine
    BudgetComparisonService ..> BudgetComparisonReport : 生成
    ProfitAnalysisService ..> ProfitReport : 生成
    ProfitAnalysisService ..> VarianceReport : 入力
    ProfitReport *-- ProjectProfitLine
```

**分析の計算規則**

- 差異 = 実績金額 − 予算金額(符号付き)
  - コスト(加工費・外注費・期間費用): 正 = 予算超過 = **不利差異**(`IsAdverse`)
  - 売上高: 正 = 売上超過 = **有利差異**(`IsFavorable`)
- 突き合わせ粒度: (区分, 案件) または (期間費用, 費目, 明細名)。**分析・集計は半期粒度**
  (明細は月次入力もできるが半期合計に畳んで比較する。Issue #5)。
  同一キーの実績は合算。計画にない期間費用の明細名の実績は「予定外」(Issue #7)
- 損益:
  - 課全体 = 売上高 −(加工費 + 外注費 + 期間費用)。利益率 = 損益 ÷ 売上高(売上高0は null)
  - 案件別 = 売上高 − 加工費 − 外注費(期間費用は課共通のため配賦しない)
  - **案件別損益の合計 − 期間費用 = 課全体の損益**(整合性はテストで担保)
- 部集計(DivisionBudgetSummaryService): 配下課の VarianceReport を区分別・損益で合計する。
  承認済み予算のない課は合計から除外し未策定として課別内訳に表示する。「部合計 = 課別内訳の合計」。
  課別内訳(DepartmentSummaryLineDto)は課ごとの区分別内訳(Categories)も持ち、
  フロントの「予算(計画)」タブで課ごとの予算比較グリッド(粗利率つき)として表示する
- 部承認(DivisionBudgetApproval): (部, 半期) ごとの承認レコード。配下の全課が承認済み予算を
  持つときのみ承認可能(条件判定は Application 層)。取り消し可。課の承認フローとは独立で、
  部承認後に課が改定しても部承認は残る
