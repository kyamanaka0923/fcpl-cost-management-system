# DB スキーマ変更の手順の知識

総合原価管理システム(予実管理)の SQLite のスキーマを変えるときの手順。[D] 実装・[E] 検証で使う。
出どころはリポジトリ(main)の `ci-and-env` スキルの「DBスキーマ変更の手順」。正本はそのファイルで、この知識はその写し。正本と食い違いを見つけたら、食い違いをレポートに書く(正本は書き換えない)。

## 方針

- 旧世代のスキーマ(2026-07-11 の課 × 半期の再構築より前)からのデータ移行はしない。旧世代のテーブルは起動のときに破棄して、新しいスキーマで作り直す(`DropLegacyTables`)
- 例外: 現世代のスキーマの中での変更(制約の緩和、要らないカラムの削除など、既存のデータが衝突しない変更)は、データを保ったまま移行する。現世代のデータを消してはいけない

## 旧世代のテーブルを作り直すとき

1. `DatabaseInitializer` の CREATE TABLE を新しいスキーマにする
2. 同じ名前のテーブルの構造が変わるなら、`DropLegacyTables` に判定を足す。`pragma_table_info` で旧世代にしかないカラムを見つけたら DROP する。旧世代専用のテーブルは無条件に `DROP TABLE IF EXISTS`(子 → 親の順)
3. 破棄の処理は冪等にする(2 回実行しても壊れない。新しいスキーマの既存のデータには触れない)
4. `tests/integration/backend/Infrastructure` の `SchemaResetTests` にテストを足す: 旧スキーマを生の SQL で再現 → Initialize → 旧テーブルが消え、新スキーマとシードがあること。冪等性(2 回実行)。新スキーマのデータが残ること
5. README の「スキーマの作り直し」節を直す

## 現世代のスキーマの中で変えるとき(データを保つ)

- カラムを足すだけなら、`pragma_table_info` に列がなければ `ALTER TABLE … ADD COLUMN …` でよい(作り直さない)
- 制約(UNIQUE など)を変えるときは、テーブルを作り直す。RENAME → CREATE → INSERT SELECT → DROP をトランザクションの中で行い、冪等にする
- 判定は実物で行う: カラムの有無は `pragma_table_info`、制約の有無は `sqlite_master.sql` の中身
- 子テーブルが FK で参照しているテーブルを作り直すときは、作り直しの間だけ `PRAGMA foreign_keys=OFF`(トランザクションの外で設定する)にし、create-new → copy → drop-old → rename の順で既存の id を保つ(id が保たれるので子の行はそのまま有効)
- 新しいテーブルは `CREATE TABLE IF NOT EXISTS` で足りる(移行は要らない)
- 移行が連なるときは、後段も前段の結果に対して冪等にする(該当の列・制約がなければ何もしない)
- テストは `SchemaResetTests` と同じ形で、移行前のスキーマを再現して、データが残ること・2 回実行しても壊れないことを確かめる

## これまでの例(いずれも順に適用される)

| 移行 | 何をしたか | 判定 |
|---|---|---|
| `MigrateProjectCodeUniqueness`(Issue #1) | 案件コードの一意制約を全体 → 課ごとに変える(作り直し) | `sqlite_master.sql` に `UNIQUE (department_id, code)` があるか |
| `MigrateDropProjectStatus`(Issue #2) | 案件の `status` 列を消す(作り直し) | `pragma_table_info` に `status` 列が残っているか |
| `MigrateAddActualMonth`(Issue #5) | `actual_entries` に `month INTEGER NULL` を足す(ALTER ADD COLUMN) | `pragma_table_info` に `month` 列がないか |
| `MigrateAddPeriodDetail`(Issue #7) | `department_budget_lines` は UNIQUE に `period_detail` を含めるため作り直し(FK の子 `department_budget_line_months` があるので `foreign_keys=OFF` で id を保つ)。`actual_entries` は ALTER ADD COLUMN。既存の行は `period_detail=''`(費目一括) | `period_detail` 列の有無 |
