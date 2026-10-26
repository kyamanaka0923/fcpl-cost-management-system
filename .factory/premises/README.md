# 前提の記録(premises)

[C] 仕様・[D] 実装に着手したエージェントが、**前提にした上流のファイルとその版**を書く(導入計画 §17.2)。
承認したあとで上流(要件・仕様・ナレッジ)が変わったら、その作業は古い前提の上に立っていることになる。
フェーズ4 の CI `upstream-approved` が、ここに書いた版と main の版を比べて検査する。

## 書き方

`tools/factory/premises.sh` で作る。版は **main にある**ファイルの blob SHA(承認済みのものだけが前提になれる)。

```bash
bash tools/factory/premises.sh 46 impl \
  requirements/budget/FR-4.8.md \
  knowledge/business-rules/BR-BUD-007.md \
  specs/rules/SPEC-001.md
```

```yaml
issue: 46
stage: impl                 # spec | impl
factory_version: factory-v1
recorded_at: 2026-10-26T12:00:00+09:00
premises:
  - path: requirements/budget/FR-4.8.md
    blob: 3b18e512dba79e4c8300dd08aeb37f8e728b8dad
```

- 1 作業項目 1 ファイル(`ISSUE-<n>.yml`)。[D] で書き直すときは、[C] の前提も含めて書く
- 前提が変わったと分かったら(main で上流が変わった)、作業を止めて Issue に `needs-answer` を付ける。勝手に書き換えて進めない
