---
name: domain-design
description: ドメインモデルの変更・追加・設計判断を行うときに必ず読む。集約の追加、不変条件の変更、課予算のバージョン管理、区分(売上高/加工費/外注費/期間費用)と案件・費目の対応付け、分析サービスの追加など、backend/src/CostManagement.Domain に触れる作業全般が対象。
model: claude-opus-4-8
---

# ドメイン設計スキル

**利用モデル**: 設計判断を伴うため `claude-opus-4-8` 以上を推奨(大規模なモデル変更は Fable 5)。
機械的なリネームや復元ファクトリの修正だけなら `claude-sonnet-5` で可。

## このシステムのドメインの決定事項(再検討しない)

- **組織は 部(Division)> 課(Department)の2階層**。課は必ず1つの部に属する
  (Department が DivisionId を持つ)。部は予算を策定せず、配下課の予実を合計する集計ビュー。
  部集計は `DivisionBudgetSummaryService`(課別 VarianceReport を合計)。「部合計 = 課別内訳の合計」が不変条件。
  承認済み予算のない課は部合計に含めず未策定として内訳表示(Application 層)
- **部承認(DivisionBudgetApproval)** は (部, 半期) ごとの集約(レコードの有無 = 承認状態)。
  配下の全課が承認済み予算を持つときのみ承認可能で、取り消せる(条件判定は Application 層)。
  課の承認フローとは**完全に独立**(DepartmentBudgetService は無変更)。部承認後に課が改定しても部承認は残る
- **管理単位は 課(Department)× 半期(FiscalHalf: yyyy-H1/H2)**。
  2026-07-11 の要件変更でプロジェクト単位予算から再構築済み(docs/requests/ 参照)
- **予算は単一集約 DepartmentBudget**。売上高(Revenue)・加工費(Processing)・
  外注費(Outsourcing)・期間費用(PeriodCost)の4区分を1つの予算としてまとめて承認する
  (旧世代の「売上と原価の別集約・品目名の緩い結合」は廃止)
- **明細は金額のみ**。半期一括に加えて**明細ごとに月次入力も可**(Issue #5)。
  `BudgetLine` は月別金額 `MonthlyAmounts`(月インデックス1..6→Money)を持ち、`IsMonthly` は
  月別金額の有無で判定。`Amount`(半期合計)は月次なら月別の合計。`UpsertProjectLineMonthly` /
  `UpsertPeriodCostLineMonthly` で月次化、半期一括の Upsert で月次モードは解除される。
  `ActualEntry` は計上月 `Month`(1..6 or null)を持つ。月の検証は `HalfMonths.Validate`。
  **集計・差異分析・損益・部集計はすべて半期粒度のまま**(月次は入力の内訳にすぎない)。
  数量×単価は廃止済み・復活させない(価格差異・数量差異の分解は存在せず、差異 = 実績金額 − 予算金額)
- **明細キーの排他**: 売上高・加工費・外注費 = (区分, 案件ID)で案件必須・費目不可 /
  期間費用 = (期間費用, 費目コード)で費目必須・案件不可。
  検証は `BudgetCategories.ValidateKey`(BudgetLine と ActualEntry の両方から使う)
- **課の区分合計 = 常に明細合計**(`CategoryTotal`)。ヘッダに金額を持たないことで
  課レベルの直接入力を構造的に不可にしている。この構造を崩さない
- **案件(Project)は課に属するマスタ**(DepartmentId 参照)。予算策定単位ではなく明細の内訳次元。
  「明細の案件は同一課所属」の検証は Application 層(DepartmentBudgetService/ActualEntryService)。
  **案件コードは課ごとに一意**(別の課では同じコード可)。`IProjectRepository.FindByCodeAsync`
  は `(DepartmentId, code)` でスコープし、DB は `UNIQUE (department_id, code)`(Issue #1)。
  **案件はライフサイクル状態(終了/Completed 等)を持たない**(Issue #2 で ProjectStatus/Complete を廃止。
  復活させない)。案件は明細の内訳次元にすぎず、予算承認で明細が確定する。
  **案件のコード・名称は登録後も編集可**(`Project.Edit(code, name)` / `ProjectService.UpdateAsync`。Issue #3)。
  他集約(BudgetLine/ActualEntry)は案件を **`ProjectId`(生成時採番の GUID)**で参照するため、
  コード変更で参照は壊れない。編集時も課ごとのコード一意性を Application 層で検証(自分自身は除外)
- **費目(CostElement)は期間費用専用のシステム全体で共通なマスタ**(課ごとの設定ではない)。
  管理はフロントの専用画面 `/cost-elements`(課の予算編集からは選択のみ)。
  シードは人件費(PERSONNEL)・ライセンス費(LICENSE)、マスタで拡張可能。
  原価要素分類(CostElementType)は廃止済み
- **予算のバージョン管理**: v1=当初、改定は明細コピーで新バージョン起票。
  Draft→Approved→Superseded。承認済みは編集不可。ドラフトは同一(課, 半期)に1件。
  明細なしは承認不可。承認時に同一(課, 半期)の旧承認版を Supersede するのは Application 層の責務
- 同一キーは上書き(Upsert)。実績(ActualEntry)は同一キーに複数計上でき、分析時に合算する
- **損益**: 課全体 = 売上高 −(加工費+外注費+期間費用)/ 案件別 = 売上高 − 加工費 − 外注費
  (期間費用は課共通、案件に配賦しない)。「案件別損益の合計 − 期間費用 = 全体の損益」が不変条件
- 差異の符号: コスト系は正=不利、売上高は正=有利(VarianceLine.IsFavorable/IsAdverse が区分で分岐)

## 戦術パターンの規約(アーキテクチャテストで強制される)

- 集約ルート・エンティティ・ドメインサービスは `sealed`
- 値オブジェクトは `readonly record struct`(Money, FiscalHalf, 型付きID, CostElementCode)
- 集約に公開セッターを置かない。状態変更はドメインメソッド(`Approve()` 等)のみ
- リポジトリはポート(interface)としてドメイン層に定義。実装はインフラ層のみ。
  **ポート数(現在7)・VO列挙・集約列挙は ArchitectureTests.cs に固定値で書かれている**ため、
  集約を増減したら必ず同時に更新する
- ドメイン例外は `DomainException`(日本語メッセージ)。WebApi が 400 に変換する。
  未検出は Application 層の `NotFoundException` → 404
- 永続化からの復元は `Restore(...)` 静的ファクトリ(検証をスキップして状態を再構築。
  ただし BudgetLine の区分×案件/費目の排他検証は Restore 経由でも internal ctor で走る)
- 分析はステートレスなドメインサービス + イミュータブルな record レポート。
  「案件別の合計 = 全体」のような整合性はテストで担保する

## 変更時の必須手順

1. ドメイン変更 → `CostManagement.Domain.Tests` を先に更新(日本語テスト名)
2. Restore ファクトリ・リポジトリ・DTO・AnalysisService への波及を必ず追う
3. スキーマが変わる場合は docs-update スキルと ci-and-env スキルの手順に従う
4. `docs/DESIGN.md` のクラス図・不変条件表を同期する
5. ArchitectureTests.cs の固定値(ポート数・VO列挙・集約列挙)を確認する
