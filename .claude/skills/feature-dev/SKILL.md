---
name: feature-dev
description: 機能追加・変更の標準ワークフロー。バックエンド(ヘキサゴナル各層)とフロントエンド(React)の実装手順、レイヤごとの置き場所、コーディング規約、コミット前の検証手順。新機能・画面追加・API追加のタスクで必ず読む。
model: claude-sonnet-5
---

# 機能開発スキル

**利用モデル**: 通常の機能追加は `claude-sonnet-5`。
ドメインモデル自体を変える場合は先に domain-design スキル(`claude-opus-4-8`)を適用。

## 実装順序(この順で層を通す)

1. **Domain** (`backend/src/CostManagement.Domain`) — 集約/値オブジェクト/ドメインサービス + ドメインテスト
2. **Application** (`.../CostManagement.Application`) — ユースケースサービス + DTO(`Dtos.cs` に集約)
3. **Infrastructure** (`.../CostManagement.Infrastructure`) — Dapper リポジトリ、スキーマ(`DatabaseInitializer`)、DI 登録(`DependencyInjection.cs`)
4. **WebApi** (`.../CostManagement.WebApi/Program.cs`) — Minimal API エンドポイント追加
5. **Frontend** (`frontend/src`) — `api.ts` に型+クライアント → ページ/コンポーネント → `App.tsx` ルート
6. **テスト**(testing スキル参照)→ ドキュメント(docs-update スキル参照)

## 各層の規約

- DTO は camelCase JSON で自動シリアライズされる(ASP.NET Core 既定)。フロントの型と一致させる
- Dapper: decimal/Guid/DateTime は `SqliteConnectionFactory` の型ハンドラで TEXT 往復。
  金額カラムは TEXT。明細の `project_id` / `element_code` は区分により排他で、
  **未使用側は DB では `''` として保存し、読み出し時に null へ読み替える**
  (SQLite の UNIQUE 制約で NULL が重複可になる問題の回避を兼ねる)
- 半期は DB では `fiscal_half TEXT`("2026-H1" 形式 = `FiscalHalf.ToString()`)
- リポジトリの検索は列エイリアス付き SQL + 入れ子 record Row → `Restore(...)` で集約復元。
  集約更新は「ヘッダUPDATE + 明細DELETE→INSERT(洗い替え)」をトランザクションで行う
- エラー方針: 不変条件違反 = `DomainException`(→400)、未検出 = `NotFoundException`(→404)。
  メッセージは日本語でユーザーに見せられる文にする
- フロント: 依存追加は最小限(React/Router のみ。UIライブラリ・チャートライブラリは使わず手書きSVG)。
  金額表示は `formatYen`/`formatSignedYen`、区分名は `categoryLabel`、半期は `halfLabel`。
  対象半期は `?half=` クエリでページ間を引き回す(既定は `currentFiscalHalf()`)。
  有利/不利の色はコスト=正が赤(adverse)、売上高・損益=正が緑(favorable)。
  ダークモード対応必須(CSS変数)
- チャートを追加・変更するときは dataviz スキル(バンドル)を先に読み込み、検証済みパレット
  (`frontend/src/styles.css` の CSS 変数)を使う

## コミット前チェックリスト

```bash
cd backend && dotnet test          # 全テスト(ドメイン/アプリ/インフラ/API E2E/アーキテクチャ)
cd frontend && npm run build       # 型チェック + ビルド
cd frontend && npm run test:e2e    # 画面を触った場合(PW_CHROMIUM=/opt/pw-browsers/chromium が必要な環境あり)
```

- コミットメッセージは日本語で「何を・なぜ」。ブランチは `claude/cost-management-system-qvc368`
  (指示がある場合はそれに従う)。プッシュ後は GitHub Actions の結果を確認する(ci-and-env スキル)
