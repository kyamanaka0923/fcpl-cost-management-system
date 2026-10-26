---
name: feature-dev
description: 機能追加・変更の標準ワークフロー。バックエンド(ヘキサゴナル各層)とフロントエンド(React)の実装手順、レイヤごとの置き場所、コーディング規約、コミット前の検証手順。新機能・画面追加・API追加のタスクで必ず読む。
model: claude-sonnet-5
---

# 機能開発スキル

**利用モデル**: 通常の機能追加は `claude-sonnet-5`。
ドメインモデル自体を変える場合は先に domain-design スキル(`claude-opus-4-8`)を適用。

## 工場の中での位置

このスキルは工程 [D] 実装(`implementer` エージェント)で使う。着手してよいのは、Issue が `stage:仕様確定`(`gate2-approved`)のときだけ
(`factory-line` スキル)。作るものは承認済みの仕様(`specs/rules/`・`docs/api/openapi.json`)と要件の `.feature` で決まっている。
仕様にないことを決めなければ進めないときは、作業を止めて Issue に `needs-answer` を付ける。`requirements/`・`knowledge/`・`specs/` は変えない。

## 実装順序(この順で層を通す)

1. **Domain** (`backend/src/CostManagement.Domain`) — 集約/値オブジェクト/ドメインサービス + ドメインテスト
2. **Application** (`.../CostManagement.Application`) — ユースケースサービス + DTO(`Dtos.cs` に集約)
3. **Infrastructure** (`.../CostManagement.Infrastructure`) — Dapper リポジトリ、スキーマ(`DatabaseInitializer`)、DI 登録(`DependencyInjection.cs`)
4. **WebApi** (`.../CostManagement.WebApi/Program.cs`) — Minimal API エンドポイント追加
5. **Frontend** (`frontend/src`) — `api.ts` に型+クライアント → ページ/コンポーネント → `App.tsx` ルート
6. **テスト**(testing スキル参照)→ ドキュメント(docs-update スキル参照)

## 各層の規約

- DTO は camelCase JSON で自動シリアライズされる(ASP.NET Core 既定)。数値は JSON の数値のみ受け付ける
  (`NumberHandling.Strict`)
- **API の型は OpenAPI から生成する**: DTO・エンドポイントを変えたら
  `cd backend && UPDATE_OPENAPI=1 dotnet test --filter OpenApi`(`docs/api/openapi.json` を更新)→
  `cd frontend && npm run gen:api`(`src/api-schema.d.ts` を再生成)。`api.ts` の型は生成型から導出し
  (区分などの文字列は `Override` で4値に絞る)、手書きの interface を増やさない。
  作成系(201)・204 のエンドポイントは `.Produces<T>()` で応答型を明示する
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
  差異の色はコスト=正が赤(利益が減る / adverse)、売上高・損益=正が緑(利益が増える / favorable)。
  ダークモード対応必須(CSS変数)
  エラーは各ページの `error`/`setError` state を **`<Toast message={error} onClose={() => setError(null)} />`**
  (`components/Toast.tsx`・画面右下固定・自動消去)で表示する。最上部バナー(`.error-banner`)は
  スクロール時に気づけないため廃止(Issue #4)。新規ページも Toast を使う
- チャートを追加・変更するときは dataviz スキル(バンドル)を先に読み込み、検証済みパレット
  (`frontend/src/styles.css` の CSS 変数)を使う

## 静的解析

- バックエンドは `backend/Directory.Build.props` で `AnalysisLevel=latest-recommended` + **警告をエラー扱い**。
  ロケール依存の `ToString`/`Parse`(CA1305)、比較方法未指定の文字列比較(CA1310)などはビルドが通らない。
  ルールを外すときはリポジトリ直下の `.editorconfig` に**理由のコメントつき**で書く(抑制属性を散らさない)
- `dotnet format` は既存コードの意図的な桁揃えを崩すため CI では強制しない(`.editorconfig` はエディタ用)
- フロントは ESLint + Prettier(CI で検査)

## コメント規約

- C# は XML コメント + `<summary>`。ドメイン層は日本語、他層は英語。
- クラス、メソッドには必ず summary コメント。パラメータ・戻り値のコメントは必要に応じて。
- フロントは JSDoc コメント + `/** ... */`。型定義・関数・コンポーネントに必ずコメント。

## コミット前チェックリスト

```bash
cd backend && dotnet test          # 全テスト(ドメイン/アプリ/インフラ/API E2E/アーキテクチャ)
cd frontend && npm run build       # 型チェック + ビルド
cd frontend && npm run lint && npm run format:check   # ESLint + Prettier(書式は npm run format で直す)
cd tests/unit/frontend && npm test  # 画面を触った場合(フロントエンド単独。速い)
cd tests/e2e/browser && npm test    # 画面を触った場合(PW_CHROMIUM=/opt/pw-browsers/chromium が必要な環境あり)
```

- コミットメッセージは日本語で「何を・なぜ」。ブランチは `impl/<Issue番号>-<名前>`(main から切る)。
  前提を `.factory/premises/ISSUE-<n>.yml` に書く(`bash tools/factory/premises.sh`)。
  プッシュ後は GitHub Actions の結果を確認する(ci-and-env スキル)。マージは人がする
