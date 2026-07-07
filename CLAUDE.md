# CLAUDE.md

総合原価管理システム(予実管理)。.NET 10 ヘキサゴナル + DDD / React 19 / SQLite + Dapper。
詳細は README.md、設計は docs/DESIGN.md、操作は docs/MANUAL.md。

## 作業前に必ず該当スキルを読むこと

このプロジェクトの規約・決定事項・落とし穴は `.claude/skills/` に種類別へ集約している。
**該当する作業を始める前に必ず対応スキルを読む**(過去のセッションで確立済みの内容を再検討・再発明しない)。

| スキル | 対象作業 | 推奨モデル |
|---|---|---|
| `domain-design` | ドメインモデルの変更・設計判断 | claude-opus-4-8 以上 |
| `feature-dev` | 機能追加・API追加・画面追加 | claude-sonnet-5 |
| `testing` | テストの追加・修正・実行 | claude-sonnet-5 |
| `docs-update` | README/MANUAL/DESIGN の同期 | claude-haiku-4-5 |
| `ci-and-env` | CI・環境・スキーマ移行・プッシュ運用 | claude-sonnet-5 |

## 絶対のルール(スキルより先に知るべき最小限)

- 明細は**金額のみ**(数量×単価は廃止済み。復活させない)
- 売上と原価は**品目名の緩い結合**(ID参照しない)。品目未指定の原価 = 共通費
- 承認済み予算は編集不可。変更は改定版(新バージョン)で
- テスト名・エラーメッセージ・コミットメッセージは日本語
- ブランチは `claude/cost-management-system-qvc368`。プッシュ後は GitHub Actions の結果を確認
