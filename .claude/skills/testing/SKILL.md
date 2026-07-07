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
| Application.Tests | ユースケース | `UseCaseFixture`(一時SQLite+本物リポジトリ+`FixedClock`) |
| Infrastructure.Tests | 永続化往復・旧スキーマ移行 | `RepositoryFixture`(一時SQLite) |
| E2E.Tests | HTTP経由の業務フロー | `WebApplicationFactory<Program>`(`ApiFixture`) |
| Architecture.Tests | 依存ルール | NetArchTest |
| frontend/e2e | ブラウザ操作 | @playwright/test(webServerで両サーバ自動起動) |

- 新しいユースケース → Application.Tests に業務フローとして追加
- 新しいリポジトリ/カラム → Infrastructure.Tests にラウンドトリップを追加
- スキーマ変更 → LegacyMigrationTests に移行+冪等性テストを追加
- 一時DBは `Path.GetTempPath()` に GUID 名で作り Dispose で削除。テスト間で共有しない

## 既知の落とし穴(このセッションで実際に踏んだもの)

- **アーキテクチャテストの空振り**: 「依存しないこと」の検証は検出器が壊れていても通る。
  ポジティブコントロール(リポジトリがDapperに依存して**いる**ことの検出)を必ず維持する。
  NetArchTest は入れ子型(record Row)も対象に含むため、`HaveNameEndingWith("Repository")` 等で絞る
- **Playwright strict mode**: 金額は明細行と合計行、統計タイルとテーブルに重複して表示される。
  `.first()` か行スコープで特定する。バッジは `.locator('.badge', { hasText: ... })`。
  説明文にも同じ語が出る(「失効」等)のでテキスト一致は要注意
- **null キーの Dictionary**: `GroupBy(x => x.NullableKey).ToDictionary(...)` は null キーで落ちる。
  null は Where で分離して別集計する(ProfitAnalysisService 参照)
- WebApplicationFactory を使うため `Program.cs` 末尾の `public partial class Program {}` を消さない

## 実行方法

```bash
cd backend && dotnet test                                  # バックエンド全部
cd frontend && npm run test:e2e                            # ブラウザE2E
# このリモート環境では: CI= PW_CHROMIUM=/opt/pw-browsers/chromium npx playwright test
```

カバレッジ目標: 全体85%以上を維持(現状 89.9%)。CI の実行サマリで確認できる。
