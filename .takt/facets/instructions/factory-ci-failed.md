Report Directory の `ci-wait.md` と `triage.md` を読み、失敗した CI の原因を集めて、その工程の書く step に渡してください。ファイルは変えず、直しもしません。

**手順:**

1. `ci-wait.md` の失敗した必須チェックごとに、実行の ID を調べ(`gh pr checks <番号> --json name,state,link` のリンク、または `gh run list --branch <ブランチ> --json databaseId,name,conclusion,headSha`)、`gh run view <run-id> --log-failed` で失敗のログを読む
2. チェックごとに、失敗したジョブ・ステップ、エラーの要点(メッセージ、ファイルと行、失敗したテストの名前)、原因の見立て(この工程の変更のどこが原因か。変更と関係がなさそうならその根拠)をまとめる。見立てが確かめられないときは「未確認」と書く
3. 工場のゲートの検査(role-guard・gate-check・作業項目を途中で閉じない など)が落ちたときは、検査の出力をそのまま引用する
4. `triage.md` の工程で、戻す書く step を決める: [A] は knowledge-write、[B] は requirements-write、[C] は spec-write、[D] と [D] の続きは implement
