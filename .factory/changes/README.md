# 工場の変更の記録(.factory/changes/)

工場の設定(`.claude/`・`AGENTS.md`・`CLAUDE.md`・`.github/workflows/`・`tools/factory/`)を変える PR は、ここに**変更 1 件につき 1 ファイル**を足す。
CI の「工場の版の更新(factory-version)」が検査する。変更の種類(A〜D)と版の上げ方は [`../CHANGELOG.md`](../CHANGELOG.md)。

1 つの表に行を足すと、工場の PR が同時に 2 つ開いたときに必ず衝突する(#62 と #64)。ファイルを分けて、PR ごとに書く場所を別にしている(#66)。

## 書き方

ファイル名は `<Issue番号>.md`。同じ Issue で 2 つ目の工場の PR を出すときは `<Issue番号>-<名前>.md`。

```markdown
---
issue: 63
kind: A
---
ブラウザE2E の Chromium を、ヘッドレスシェルだけ・版ごとのキャッシュ・OS の依存なしで入れる(CI の待ちを短くする)
```

| 項目 | 書くこと |
|---|---|
| `issue` | 工場の改善の Issue の番号(ファイル名の番号と同じ) |
| `kind` | 変更の種類 `A`〜`D`(Issue の `factory:A`〜`factory:D` と同じ) |
| 本文 | 何を変えたかと、なぜかを 1〜2 文で |

- PR の番号は書かない(PR を作る前は分からない。Issue から PR をたどれる)
- マージした記録は消さない(検査が止める)。直すときは本文を書き換える

## 一覧

```bash
node tools/factory/cli.mjs changes   # 記録を確かめて、Issue の番号の順の表を出す
```

factory-v1 の導入中(#32〜#62)の記録は、[`../CHANGELOG.md`](../CHANGELOG.md) の表に残している(追記しない)。
