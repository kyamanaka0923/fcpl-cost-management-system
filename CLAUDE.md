# CLAUDE.md

@AGENTS.md

総合原価管理システム(予実管理)。.NET 10 ヘキサゴナル + DDD / React 19 / SQLite + Dapper。
詳細は README.md、設計は docs/DESIGN.md、操作は docs/MANUAL.md。
**工場(ソフトウェアファクトリー)の共通の指示は AGENTS.md**。ここには Claude Code 固有のことだけを書く。

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
| `issue-response` | GitHub Issue を確認し未対応のものを対応する運用 | claude-opus-5 |

## 絶対のルール(スキルより先に知るべき最小限)

業務の決まりの正は `knowledge/`(用語集・業務ルール `BR-…`)。ここは要点だけ。

- 組織は **部 > 課** の2階層。予算・実績は課が持ち、部は集計ビュー(BR-ORG-001、BR-DIV-001)
- 予算は**課 × 半期の単一集約**で、4区分を1承認。売上と原価の別承認は廃止済み・復活させない(BR-BUD-001)
- 明細は**金額のみ**。数量×単価は廃止済み・復活させない(BR-BUD-007)。月次入力は内訳で、集計は半期(BR-BUD-005、BR-FH-001)
- 区分合計 = 明細合計のみ(BR-BUD-004)。期間費用は費目一括か明細名ありのどちらか一方(BR-BUD-006)
- 承認済み予算は編集不可。変更は改定版で(BR-BUD-002)
- テスト名・エラーメッセージ・コミットメッセージは日本語
- ブランチは AGENTS.md の工程別の命名(`knowledge/` `req/` `spec/` `impl/` `prep/` `factory/`)で main から切る。main へ直接プッシュしない。プッシュ後は GitHub Actions の結果を確認
- **マージ・PR の Ready 化・承認・ゲートラベルの付与はしない**。`.claude/settings.json` と `.claude/hooks/guard.sh` が止める。止められたら回避せず、人に頼む
