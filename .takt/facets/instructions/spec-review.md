工程 [C] 仕様の検証として、このブランチの差分を承認済みの要件と突き合わせてください。ファイルは書き換えません。

**手順:**

1. Report Directory の `triage.md`(Issue の要約)と `spec-write.md` を読む(報告は手がかりで、判定は実物で行う)
2. 差分を読む: `git diff "$(git merge-base origin/main HEAD)"`、`git status --porcelain`(未追跡のファイルは中身を開く)
3. `.factory/premises/ISSUE-<n>.yml` を読み、各前提の blob が `git rev-parse origin/main:<path>` と一致するかを確かめる
4. 承認済み要件(`@FR-x.y`)との対応を確かめる
   - この Issue の要件(前提の FR と、`.feature` の `@FR-x.y`)1 件ごとに、満たす API と処理ルールがあるか
   - 仕様が origin/main にない要件を指していないか
   - 要件・業務ルールにない決まりを仕様が足していないか
5. `docs/api/openapi.json` の差分の整合を確かめる
   - JSON として読めるか(`node -e "JSON.parse(require('fs').readFileSync('docs/api/openapi.json','utf8'))"` など)
   - 処理ルールの API(メソッド・パス・要求・応答・エラー)と一致するか
   - 既存のクライアント(`frontend/src` が呼ぶ API)を壊さないか(消した・型を変えた項目を `frontend/src` で探す)
6. テストの観点が要件から導かれているかを確かめる
   - 具体例の表に境界(0・1 つ下・上限・空)があり、各行にテストの観点があるか
   - 観点が `.feature` のシナリオ・業務ルールの例とつながっているか。いまのコードの写しになっていないか
7. 持ち場(`spec/` の `ROLES`)の外のファイルが変わっていないか、`knowledge/review-viewpoints.md` の「共通」と「この業務に特有のもの」を確かめる
8. 2回目以降は、前回の `spec-review.md` の指摘が直ったかを1件ずつ確かめる
