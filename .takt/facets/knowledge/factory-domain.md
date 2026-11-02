# ドメインの規約の知識

総合原価管理システム(予実管理)のドメインの決定事項と、ドメインを変えるときの規約。[C] 仕様・[D] 実装・[E] 検証で使う。
この知識が正本(スキル `domain-design` はここを読む案内)。`CLAUDE.md`・`AGENTS.md` には絶対のルールの要点だけを置く。
業務の決まりの正は `knowledge/`(用語集・業務ルール `BR-…`)。ここに書いた決定事項は再検討しない。

## プロダクトの絶対のルール

- 組織は部 > 課 の 2 階層。予算・実績は課が持ち、部は集計ビュー(BR-ORG-001、BR-DIV-001)
- 予算は課 × 半期の単一集約で、4 区分を 1 承認。売上と原価の別承認は廃止済み・復活させない(BR-BUD-001)
- 明細は金額のみ。数量 × 単価は廃止済み・復活させない(BR-BUD-007)。月次入力は内訳で、集計は半期(BR-BUD-005、BR-FH-001)
- 区分合計 = 明細合計のみ(BR-BUD-004)。期間費用は費目一括か明細名ありのどちらか一方(BR-BUD-006)
- 承認済み予算は編集不可。変更は改定版で(BR-BUD-002)
- テスト名・エラーメッセージ・コミットメッセージは日本語

## ドメインの決定事項(再検討しない)

### 組織と部

- 課(Department)は必ず 1 つの部(Division)に属する(Department が DivisionId を持つ)。部は予算を策定しない
- 部の集計は `DivisionBudgetSummaryService`(課別の VarianceReport を合計)。「部合計 = 課別内訳の合計」が不変条件。承認済み予算のない課は部合計に含めず、未策定として内訳に出す(Application 層)
- 部承認(`DivisionBudgetApproval`)は (部, 半期) ごとの集約(レコードの有無 = 承認状態)。配下の全課が承認済み予算を持つときだけ承認でき、取り消せる(条件の判定は Application 層)。課の承認フローとは完全に独立(`DepartmentBudgetService` は変えない)。部承認のあとに課が改定しても、部承認は残る

### 予算と明細

- 管理単位は課(Department)× 半期(FiscalHalf: yyyy-H1/H2)
- 予算は単一集約 `DepartmentBudget`。売上高(Revenue)・加工費(Processing)・外注費(Outsourcing)・期間費用(PeriodCost)の 4 区分をまとめて承認する
- 明細のキーは排他: 売上高・加工費・外注費 = (区分, 案件 ID) で案件必須・費目不可 / 期間費用 = (期間費用, 費目コード, 明細名) で費目必須・案件不可。検証は `BudgetCategories.ValidateKey`(BudgetLine と ActualEntry の両方から使う)
- 課の区分合計 = 常に明細合計(`CategoryTotal`)。ヘッダに金額を持たないことで、課レベルの直接入力を構造的にできなくしている。この構造を崩さない
- 同じキーは上書き(Upsert)。実績(ActualEntry)は同じキーに複数計上でき、分析のときに合算する

### 月次入力

- 半期一括に加えて、明細ごとに月次入力もできる(Issue #5)。`BudgetLine` は月別金額 `MonthlyAmounts`(月インデックス 1..6 → Money)を持ち、`IsMonthly` は月別金額の有無で判定する。`Amount`(半期合計)は、月次なら月別の合計
- `UpsertProjectLineMonthly` / `UpsertPeriodCostLineMonthly` で月次にし、半期一括の Upsert で月次は解除される
- `ActualEntry` は計上月 `Month`(1..6 か null)を持つ。月の検証は `HalfMonths.Validate`
- 集計・差異分析・損益・部集計は、すべて半期の粒度のまま(月次は入力の内訳にすぎない)
- 差異 = 実績金額 − 予算金額。価格差異・数量差異の分解はない

### 期間費用の明細名(PeriodDetail)

- 費目の中を、自由入力の明細名でさらに分けられる(Issue #7。例: ライセンス費 → "AWS" / "GitHub")
- `BudgetLine.PeriodDetail` / `ActualEntry.PeriodDetail` は null = 費目一括、非 null = 明細。1 つの費目の中で費目一括と明細は併用できない(`DepartmentBudget.EnsurePeriodModeConsistent` が二重計上を防ぐ)
- 明細名は期間費用専用で、案件別の区分に指定すると例外。費目合計 = 明細の合計。分析のキーは (区分, 案件 or 費目, 明細名)。計画にない明細名の実績は「予定外」
- マスタは作らず、自由入力の文字列(`BudgetLine.NormalizeDetail` で trim し、空は null)
- 明細名は後から変えられる(`DepartmentBudget.RenamePeriodCostDetail` / `BudgetLine.RenameDetail`。Issue #8)。金額・月次・明細 ID は保ち、同じ費目の中の重複名は拒否する。実績は明細名で疎結合なので改名に追随しない(旧名の実績は「予定外」になる)。空への改名はできない

### 案件と費目

- 案件(Project)は課に属するマスタ(DepartmentId 参照)。予算策定の単位ではなく、明細の内訳の次元。「明細の案件は同じ課に属する」の検証は Application 層(DepartmentBudgetService / ActualEntryService)
- 案件コードは課ごとに一意(別の課では同じコードでよい)。`IProjectRepository.FindByCodeAsync` は (DepartmentId, code) でスコープし、DB は `UNIQUE (department_id, code)`(Issue #1)
- 案件はライフサイクルの状態(終了 / Completed など)を持たない(Issue #2 で廃止。復活させない)
- 案件のコード・名称は登録後も編集できる(`Project.Edit(code, name)` / `ProjectService.UpdateAsync`。Issue #3)。他の集約は案件を `ProjectId`(生成時に採番する GUID)で参照するので、コードを変えても参照は壊れない。編集のときも、課ごとのコードの一意性を Application 層で確かめる(自分自身は除く)
- 費目(CostElement)は期間費用専用で、システム全体で共通のマスタ(課ごとの設定ではない)。管理は専用画面 `/cost-elements`(課の予算編集からは選ぶだけ)。シードは人件費(PERSONNEL)・ライセンス費(LICENSE)。原価要素の分類(CostElementType)は廃止済み

### バージョン管理

- v1 = 当初。改定は明細をコピーして新しいバージョンを起こす
- Draft → Approved → Superseded。承認済みは編集できない。ドラフトは同じ (課, 半期) に 1 件
- 明細のない予算は承認できない
- 承認のときに、同じ (課, 半期) の旧承認版を Supersede するのは Application 層の責務

### 損益と差異

- 課全体の損益 = 売上高 −(加工費 + 外注費 + 期間費用)。案件別の損益 = 売上高 − 加工費 − 外注費(期間費用は課で共通。案件に配賦しない)
- 「案件別損益の合計 − 期間費用 = 全体の損益」が不変条件
- 差異の符号: コスト系は正 = 利益が減る、売上高は正 = 利益が増える(`VarianceLine.IsFavorable` / `IsAdverse` が区分で分岐)。文書・画面・テストでは「有利/不利」という語を使わず、「利益が増える/減る」と書く

## 戦術パターン(アーキテクチャテストで強制される)

- 集約ルート・エンティティ・ドメインサービスは `sealed`
- 値オブジェクトは `readonly record struct`(Money、FiscalHalf、型付き ID、CostElementCode)
- 集約に公開セッターを置かない。状態の変更はドメインメソッド(`Approve()` など)だけ
- リポジトリはポート(interface)としてドメイン層に定義し、実装はインフラ層だけに置く
- ポート数(現在 7)・値オブジェクトの列挙・集約の列挙は `ArchitectureTests.cs` に固定値で書かれている。集約を増減したら同時に直す
- ドメインの例外は `DomainException`(日本語のメッセージ)。WebApi が 400 にする。未検出は Application 層の `NotFoundException` → 404
- 永続化からの復元は `Restore(...)` 静的ファクトリ(検証を飛ばして状態を組み立て直す。ただし BudgetLine の区分 × 案件・費目の排他の検証は、Restore を通しても internal ctor で走る)
- 分析はステートレスなドメインサービス + イミュータブルな record のレポート。「案件別の合計 = 全体」のような整合はテストで確かめる

## ドメインを変えるときの手順

1. ドメインのテスト(`tests/unit/backend/Domain`。日本語のテスト名)を先に直す
2. `Restore` ファクトリ・リポジトリ・DTO・分析サービスへの波及を追う
3. スキーマが変わるなら、知識「DB スキーマ変更の手順」に従う
4. `docs/DESIGN.md` のクラス図・不変条件の表を同期する
5. `ArchitectureTests.cs` の固定値(ポート数・値オブジェクトの列挙・集約の列挙)を確かめる
