# 総合原価管理システム(予実管理)

**原価・売上高の予算策定・実績計上・差異分析**を行うシステムです。
ソフトウェア開発の SE 費用管理を主なユースケースとして、明細は数量×単価ではなく**金額**で直接管理します。

- 予算(予定)はプロジェクトごとに**バージョン管理**され、四半期などの節目で何度でも改定できます
- **売上予算**は原価予算とは独立にバージョン管理・改定できます(品目 × 年月 × 金額)
- **売上対応原価の詳細定義**: 原価明細(予算・実績)に「売上対応品目」を紐付けでき、
  品目(案件)単位の粗利を予実で突き合わせられます。品目を指定しない原価は共通費として扱われます
- 予実の**差異分析**: 差異 = 実績金額 − 予算金額(原価は超過が不利、売上は超過が有利)
- **損益(粗利)分析**: 全体・品目別・月別の粗利予実と粗利率
- 予算バージョン間の**変動比較**(例: 当初予算 vs 第2四半期改定)ができます

## 技術スタック

| レイヤ | 技術 |
|---|---|
| フロントエンド | React 19 + TypeScript + Vite |
| バックエンド | .NET 10(ASP.NET Core Minimal API) |
| データベース | SQLite + Dapper |
| アーキテクチャ | ヘキサゴナルアーキテクチャ + ドメイン駆動設計(戦術パターン) |

## アーキテクチャ

ヘキサゴナル(ポート&アダプタ)構成。依存はすべて内側(Domain)に向かいます。

```
backend/
├── src/
│   ├── CostManagement.Domain/          # 中心: エンティティ・値オブジェクト・集約・
│   │   │                               #       ドメインサービス・リポジトリポート
│   │   ├── Shared/                     #   Money, AccountingPeriod(値オブジェクト)
│   │   ├── Projects/                   #   Project 集約
│   │   ├── CostElements/               #   CostElement(費目マスタ)集約
│   │   ├── Planning/                   #   CostPlan 集約(バージョン管理・承認ワークフロー)
│   │   ├── Actuals/                    #   ActualCost 集約
│   │   ├── Revenue/                    #   RevenuePlan / ActualRevenue 集約(売上)
│   │   └── Analysis/                   #   差異分析・バージョン比較のドメインサービス群
│   ├── CostManagement.Application/     # ユースケース(入力ポート)・DTO
│   ├── CostManagement.Infrastructure/  # 出力アダプタ: Dapper + SQLite リポジトリ実装
│   └── CostManagement.WebApi/          # 入力アダプタ: HTTP API(Minimal API)
└── tests/
    └── CostManagement.Domain.Tests/    # ドメイン単体テスト(xUnit)

frontend/                               # React SPA(/api を dev proxy 経由でバックエンドへ)
```

### ドメインモデル(DDD 戦術パターン)

| 要素 | 実装 |
|---|---|
| 集約ルート | `Project`, `CostPlan`, `ActualCost`, `CostElement`, `RevenuePlan`, `ActualRevenue` |
| エンティティ | `PlanLine`(費目 × 売上対応品目 × 年月 × 金額), `RevenuePlanLine`(品目 × 年月 × 金額) |
| 値オブジェクト | `Money`, `AccountingPeriod`(yyyy-MM), `ProjectId` 等の型付き ID, `CostElementCode` |
| ドメインサービス | `VarianceAnalysisService` / `RevenueVarianceAnalysisService`(予実差異分析), `PlanComparisonService` / `RevenuePlanComparisonService`(バージョン間比較), `ProfitAnalysisService`(品目別・月別の損益突き合わせ) |
| リポジトリ(ポート) | `IProjectRepository`, `ICostPlanRepository`, `IActualCostRepository`, `ICostElementRepository`, `IRevenuePlanRepository`, `IActualRevenueRepository` |
| ドメイン例外 | `DomainException`(不変条件違反 → HTTP 400 に変換) |

### 予算のライフサイクル

```
当初予算 v1 (Draft) ── 承認 ──> v1 (Approved)
                                    │ 四半期改定(明細を引き継いでドラフト起票)
                                    v
                         v2 (Draft) ── 承認 ──> v2 (Approved)
                                                 v1 は Superseded(履歴として保持)
```

主な不変条件:

- 承認済み・失効済みの予算は編集不可(改定版の作成が必要)
- 明細のない予算は承認不可
- 策定中のドラフトは同時に 1 つまで
- 同一プロジェクト内でバージョン番号は単調増加
- 新バージョンの承認により、旧承認版は自動的に失効(Superseded)

### 差異分析(変動分析)

明細は金額で管理されるため、差異は「実績金額 − 予算金額」で符号付きに算出します。

- **原価**: (費目, 売上対応品目, 年月) の粒度で突き合わせ。正 = 予算超過 = 不利差異
- **売上**: (品目, 年月) の粒度で突き合わせ。正 = 売上超過 = 有利差異(原価と逆)

同一キーに複数の実績がある場合は合算されます。予算にない実績は「予定外」として報告されます。

### 損益(粗利)分析と売上対応原価

原価明細の「売上対応品目」と売上の品目を突き合わせ、`ProfitAnalysisService` が
**品目(案件)単位の粗利予実**を算出します。売上対応品目のない原価は「共通費」行に
集計され、品目別損益の合計は常に全体の損益と一致します。あわせて粗利率・月別内訳も
算出します(対象は最新の承認済み売上予算・原価予算)。

### スキーマ移行

旧スキーマ(数量×単価で明細管理していた世代)のデータベースは、起動時に自動移行されます
(金額 = 数量 × 単価で引き継ぎ、売上対応品目は未設定=共通費扱い)。

## 実行方法

### バックエンド

```bash
cd backend
dotnet run --project src/CostManagement.WebApi
# => http://localhost:5100 (初回起動時に costmanagement.db を自動作成・費目マスタをシード)
```

### フロントエンド

```bash
cd frontend
npm install
npm run dev
# => http://localhost:5173 (/api は localhost:5100 にプロキシ)
```

### テスト

```bash
cd backend
dotnet test
```

## API 概要

| メソッド/パス | 説明 |
|---|---|
| `GET/POST /api/projects` | プロジェクト一覧・作成 |
| `POST /api/projects/{id}/complete` | プロジェクト完了 |
| `GET/POST /api/cost-elements` | 費目マスタ一覧・追加 |
| `GET/POST /api/projects/{id}/plans` | 予算バージョン一覧・ドラフト起票(初回は当初予算、以降は改定版) |
| `GET /api/plans/{planId}` | 予算詳細(明細含む) |
| `PUT/DELETE /api/plans/{planId}/lines` | 予算明細の登録(upsert)・削除 |
| `POST /api/plans/{planId}/approve` | 予算承認(旧承認版は自動失効) |
| `GET/POST /api/projects/{id}/actuals` | 実績一覧・計上 |
| `DELETE /api/actuals/{actualId}` | 実績取消 |
| `GET/POST /api/projects/{id}/revenue-plans` | 売上予算バージョン一覧・ドラフト起票 |
| `GET /api/revenue-plans/{planId}` | 売上予算詳細(明細含む) |
| `PUT/DELETE /api/revenue-plans/{planId}/lines` | 売上予算明細の登録(upsert)・削除 |
| `POST /api/revenue-plans/{planId}/approve` | 売上予算承認(旧承認版は自動失効) |
| `GET/POST /api/projects/{id}/actual-revenues` | 売上実績一覧・計上 |
| `DELETE /api/actual-revenues/{actualId}` | 売上実績取消 |
| `GET /api/projects/{id}/variance?planId=&from=&to=` | 原価の予実差異分析(バージョン・期間指定可) |
| `GET /api/projects/{id}/revenue-variance?planId=&from=&to=` | 売上の予実差異分析 |
| `GET /api/projects/{id}/plan-comparison?baseVersion=&targetVersion=` | 原価予算バージョン間比較 |
| `GET /api/projects/{id}/revenue-plan-comparison?baseVersion=&targetVersion=` | 売上予算バージョン間比較 |
| `GET /api/projects/{id}/profit?from=&to=` | 損益(粗利)予実サマリ(全体・品目別・月別) |
| `GET /api/projects/{id}/revenue-items` | 売上対応品目の候補一覧(入力補完用) |

## 画面

- **プロジェクト一覧 / 詳細** — プロジェクト登録、原価・売上それぞれの予算バージョンの一覧・改定・承認
- **予算編集** — 原価予算(費目 × 売上対応品目 × 年月 × 金額)/ 売上予算(品目 × 年月 × 金額)のドラフト明細編集。売上対応品目は売上側の品目から入力補完
- **実績入力** — 原価実績(売上対応品目つき)・売上実績の計上(同一キーへの複数計上に対応)
- **予実差異分析・損益** — タブ構成: 原価差異 / 売上差異 / 損益(粗利)。月別チャート、費目・品目別差異チャート、品目別損益テーブル(共通費行を含む)、月別内訳
- **予算バージョン比較** — 原価予算・売上予算それぞれで任意の 2 バージョン間の増減を明細単位で比較
