---
name: testing
description: テストの追加・修正・実行の規約。5種類のバックエンドテスト(ドメイン/アプリケーション/インフラ/API E2E/アーキテクチャ)とブラウザE2E(Playwright)の書き方、日本語テスト名の方針、既知の落とし穴。テストを書く・直す作業で必ず読む。
model: claude-sonnet-5
---

# テストスキル

**利用モデル**: `claude-sonnet-5`。テスト戦略自体の再設計は `claude-opus-4-8`。

## 大原則: テスト名は日本語の文にする

テスト一覧が仕様書として読めることを最優先する。
例: `改定版を承認すると旧バージョンは自動的に失効する()`。
クラス名も業務概念(`原価予算の策定と改定` 等)。ヘルパ名も日本語可(`承認済み原価予算を作成`)。

## テストの種類と置き場所

| プロジェクト | 対象 | 方式 |
|---|---|---|
| Domain.Tests | 不変条件・計算規則 | 純粋単体 |
| Application.Tests | ユースケース | `UseCaseFixture`(一時SQLite+本物リポジトリ+`FixedClock`。日本語ヘルパ: `課を作成`/`案件を作成`/`承認済み予算を作成`) |
| Infrastructure.Tests | 永続化往復・スキーマ作り直し | `RepositoryFixture`(一時SQLite)+ `SchemaResetTests` |
| E2E.Tests | HTTP経由の業務フロー | `WebApplicationFactory<Program>`(`ApiFixture`) |
| Architecture.Tests | 依存ルール | NetArchTest |
| frontend/e2e | ブラウザ操作 | @playwright/test(webServerで両サーバ自動起動) |

- 新しいユースケース → Application.Tests に業務フローとして追加
- 新しいリポジトリ/カラム → Infrastructure.Tests にラウンドトリップを追加
- スキーマ変更 → `SchemaResetTests` に「旧テーブルの破棄・作り直し+冪等性(2回実行)+
  新スキーマの既存データ保持」のテストを追加(レガシー移行は行わない方針。ci-and-env 参照)
- 一時DBは `Path.GetTempPath()` に GUID 名で作り Dispose で削除。テスト間で共有しない

## 既知の落とし穴(このセッションで実際に踏んだもの)

- **アーキテクチャテストの空振り**: 「依存しないこと」の検証は検出器が壊れていても通る。
  ポジティブコントロール(リポジトリがDapperに依存して**いる**ことの検出)を必ず維持する。
  NetArchTest は入れ子型(record Row)も対象に含むため、`HaveNameEndingWith("Repository")` 等で絞る
- **Playwright strict mode**: 金額は明細行と合計行、統計タイルとテーブルに重複して表示される。
  `.first()` か行スコープで特定する。バッジは `.locator('.badge', { hasText: ... })`。
  説明文にも同じ語が出る(「失効」等)のでテキスト一致は要注意
- **null キーの Dictionary**: `GroupBy(x => x.NullableKey).ToDictionary(...)` は null キーで落ちる。
  null になりうるキーは Where で分離してから集計する(ProfitAnalysisService は案件系区分に
  絞ってから `ProjectId!.Value` で GroupBy している。タプルキーなら null を含んでも安全)
- WebApplicationFactory を使うため `Program.cs` 末尾の `public partial class Program {}` を消さない
- **Playwright の連続フォーム送信レース**: 送信ボタンを連打する前に完了を待つこと。
  ローカルの速いマシンでは通っても遅い CI で落ちる。実績計上のように送信後に入力欄が
  クリアされる画面なら `await expect(欄).toHaveValue('')` で完了を待ってから次へ進む
  (`reuseExistingServer` で残ったサーバを掴むと結果がぶれるので、E2E前に 5173/5100 を掃除する)

## プロパティベーステスト(CsCheck)

例示テストに加えて、ドメインとユースケースの**不変条件**は CsCheck で任意の入力に対して検証する
(C# ネイティブ・縮小付き。Domain.Tests / Application.Tests が参照)。`[Fact]` の中で
`gen.Sample(...)` / `gen.SampleAsync(...)` を呼ぶだけでよい。

| 置き場所 | クラス | 検証する性質 |
|---|---|---|
| Domain.Tests/PropertyBased/ValueObjectPropertyTests.cs | `値オブジェクトの性質` | Money の加減算の法則、FiscalHalf の文字列往復と全順序、費目コードの正規化、月 1..6 |
| 〃 DepartmentBudgetPropertyTests.cs | `課予算の性質` | **モデルベース**: 任意の編集操作列を集約と単純モデルに適用し成否・状態が一致、拒否された操作は無副作用、不変条件(区分合計=明細合計・月次合計・混在禁止)。承認後の編集拒否、改定の引き継ぎ、Restore 往復 |
| 〃 ActualEntryPropertyTests.cs | `実績計上の性質` | 計上可否の規則、明細名・備考の正規化、Restore 往復 |
| 〃 AnalysisServicePropertyTests.cs | `分析サービスの性質` | 差異=実績−予算、区分合計=明細合計、予定外判定、入力順非依存、比較の反対称性、「案件別損益−期間費用=全体損益」、「部合計=課別内訳の合計」 |
| Application.Tests/UseCasePropertyTests.cs | `ユースケースの性質` | SQLite 往復後も応答どおり読み戻せる、改定を繰り返しても承認版は常に1件、実績合計の反映、部合計は承認済み課のみ |

- 生成器は `PropertyBased/DomainGenerators.cs`(`ドメイン生成器`)。案件・費目・明細名は**小さなプール**から選び、
  上書き・衝突・混在が頻繁に起きるようにする。金額は1円単位の整数(decimal の和を厳密比較するため)
- 失敗時は `Set seed: "..."` が出る。`CsCheck_Seed=<seed> dotnet test --filter ...` で再現。
  反復回数は `CsCheck_Iter=2000` 等で一時的に増やせる(既定 100。Application は DB を作るため `iter: 25`)
- 反例表示のため、Dictionary を持つ操作 record は `ToString` を上書きする(既定では中身が出ない)
- **落とし穴**: SQLite 往復した decimal は値が同じでも scale が変わる(`100` → `100.0`)。文字列比較時は正規化する
- PBT で `BudgetLine.SetMonthly` が検証前に月別金額を書き換えていた不具合を検出・修正済み
  (回帰テスト `不正な月次金額での上書きが拒否されても既存明細の金額と月次は変わらない`)

## 実行方法

```bash
cd backend && dotnet test                                  # バックエンド全部
cd frontend && npm run test:e2e                            # ブラウザE2E
# このリモート環境では: CI= PW_CHROMIUM=/opt/pw-browsers/chromium npx playwright test
```

カバレッジ目標: 全体85%以上を維持(現状 89.9%)。CI の実行サマリで確認できる。

## ミューテーションテスト(Stryker.NET。Issue #6)

行カバレッジは「実行したか」しか見ないため、テストが**仕様を実際に検証しているか**は
Stryker.NET のミューテーションテストで補完する。ツールはローカルツールとして
`backend/.config/dotnet-tools.json` に固定(`dotnet-stryker` 4.16.0)。

- 対象は **Domain・Application 層のみ**(業務ロジックの中核。Infrastructure は SQL 文字列中心で
  ミューテーションの価値が低く遅い、WebApi は薄いホスティングのため除外)
- 設定は各テストプロジェクト直下の `stryker-config.json`
  (`CostManagement.Domain.Tests` / `CostManagement.Application.Tests`)。
  `project` で対象 src を明示(テストプロジェクトが複数 src を参照する場合の曖昧さ回避に必須)。
  レポータは html/json/markdown/cleartext。閾値 high:80 low:60 **break:0(スコアで CI を落とさない)**
- 実行(各テストプロジェクトのディレクトリで):

  ```bash
  cd backend && dotnet tool restore
  cd tests/CostManagement.Domain.Tests      && dotnet tool run dotnet-stryker
  cd tests/CostManagement.Application.Tests && dotnet tool run dotnet-stryker
  ```

- 出力は `StrykerOutput/<日時>/reports/`(gitignore 済み)。CI は `mutation` ジョブで
  markdown を実行サマリへ、html を `mutation-report` アーティファクトへ出す(**非ブロッキング**)
- 現状スコアの目安: Domain ≈ 54%、Application ≈ 54%。実行時間は各 1.5〜2 分程度
