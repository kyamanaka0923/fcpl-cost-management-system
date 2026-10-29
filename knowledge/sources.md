# 出どころの索引

ナレッジの各項目が、どこから来たかを示す。生データ(録画・文字起こし)はここに置かず、所在だけを書く。

| ID | 出どころ | 版・日付 | 所在 |
|---|---|---|---|
| SRC-REQ | 要件定義書(移行元。未移行の要件の正) | 1.0(2026-07-18) | [docs/REQUIREMENTS.md](../docs/REQUIREMENTS.md) |
| SRC-REQ-CHG-0711 | 要件変更依頼(課 × 半期への再構築) | 2026-07-11 | [docs/requests/2026-07-11-requirement-change.md](../docs/requests/2026-07-11-requirement-change.md) |
| SRC-REQ-CHG-0712 | 要件変更依頼(案件単位の編集・部の予算参照) | 2026-07-12 | [docs/requests/2026-07-12-requirement-change.md](../docs/requests/2026-07-12-requirement-change.md) |
| SRC-ADR-0002〜0016 | アーキテクチャ決定記録(番号で引く。例: SRC-ADR-0006) | 各 ADR の日付 | [docs/adr/](../docs/adr/README.md) |
| SRC-ISSUE-1 | Issue #1 案件・費目のドメイン内容を見直し | クローズ済み | GitHub Issue #1 |
| SRC-ISSUE-2 | Issue #2 課別詳細画面の改善 | クローズ済み | GitHub Issue #2 |
| SRC-ISSUE-3 | Issue #3 案件マスタの編集機能追加 | クローズ済み | GitHub Issue #3 |
| SRC-ISSUE-4 | Issue #4 エラーメッセージ表示箇所がわかりにくい | クローズ済み | GitHub Issue #4 |
| SRC-ISSUE-5 | Issue #5 計画、実績の入力単位を拡張(月次入力) | クローズ済み | GitHub Issue #5 |
| SRC-ISSUE-7 | Issue #7 期間費用詳細化(明細名) | クローズ済み | GitHub Issue #7 |
| SRC-ISSUE-8 | Issue #8 期間費用 明細の名称変更 | クローズ済み | GitHub Issue #8 |
| SRC-ISSUE-9 | Issue #9 編集UIの統一 | クローズ済み | GitHub Issue #9 |
| SRC-CODE | 実装(ドメイン層の不変条件とエラーメッセージ) | 2026-10-26 の main | `backend/src/CostManagement.Domain/`、`backend/src/CostManagement.Application/` |
| SRC-ANS-OQ-001〜003 | 未確定事項への回答(工場長・Outcome Owner) | 2026-10-26 | [open-questions.md](open-questions.md) と PR #44 のやりとり |
| SRC-ANS-OQ-004〜006 | 未確定事項への回答(工場長・Outcome Owner。初物検査 #46) | 2026-10-27 | [open-questions.md](open-questions.md) と PR #56 のコメント |
| SRC-ANS-OQ-007 | 未確定事項 OQ-007 への回答(工場長・Outcome Owner。初物検査 #46) | 2026-10-28(JST) | [open-questions.md](open-questions.md) と GitHub Issue #46 のコメント(2026-10-27 21:54 UTC / 2026-10-28 06:54 JST) |
| SRC-ANS-OQ-008 | 未確定事項 OQ-008 への回答(工場長・Outcome Owner。初物検査 #46) | 2026-10-29(JST) | [open-questions.md](open-questions.md) と GitHub Issue #46 のコメント(2026-10-29 11:13 UTC / 20:13 JST) |
| SRC-ANS-ISSUE-46-1029 | PR #70 の問いへの回答(入り方の範囲・4 つの入り方・BR-BUD-007 への書き写し。工場長・Outcome Owner) | 2026-10-29(JST) | GitHub Issue #46 のコメント(2026-10-29 10:53 UTC / 19:53 JST と 11:13 UTC / 20:13 JST) |
| SRC-ANS-ISSUE-46-1029B | PR #76 と PR #75 のレビューの問いへの回答(「打鍵」を FR-4.13 の言い方に揃える・小数点を含まない文字列は拒否しない・範囲を選んで小数点を打鍵したとき選んだ範囲を消さず前のまま。工場長・Outcome Owner) | 2026-10-29(JST) | GitHub Issue #46 のコメント(2026-10-29 12:32 UTC / 21:32 JST) |
| SRC-CODE-DBINIT | 実装(DB の初期化。いまの実装がそうなっていることだけを示す): 新しく行を入れるのは費目(cost_elements)の 2 行(PERSONNEL・LICENSE)だけ。department_budget_lines・department_budget_line_months・actual_entries に新しく行を入れない(department_budget_lines と projects の INSERT は、スキーマの移行で既存の行を写すものだけ) | 2026-10-29 の main(f62d311) | `backend/src/CostManagement.Infrastructure/Persistence/DatabaseInitializer.cs` |
| SRC-ANS-ISSUE-46-1030 | OQ-005 の確かめ方を変える回答(運用している DB はない。数える相手がないので、端数つきのデータはないとして確かめを終える(案 1)。PR #80 の問い 3 の回答 A は取り消し。工場長・Outcome Owner) | 2026-10-30(JST) | GitHub Issue #46 のコメント(2026-10-29 15:20 UTC / 2026-10-30 00:20 JST) |
| SRC-FACTORY | ソフトウェアファクトリーの文書(PROC-DEV-001 v2.7・導入計画・概要) | 2026-10-25 | リポジトリ外(作者の保管庫) |

- 既存の文書(REQUIREMENTS.md・要件変更依頼)は改訂せず、出どころとして参照する。要件を `requirements/` に移したら、移行元の行に印を付ける
- ヒアリングを行ったら、`SRC-HEAR-<日付>-<相手>` の形で、日付・相手・録画の所在だけを足す
