```markdown
# 工程の判定

## 結果
- Issue: #{番号}({タイトル}。番号が取れなければ「なし」)
- 判定: {[A] ナレッジ / [B] 要件・画面 / [C] 仕様 / [D] 実装 / [D] の続き / 進めない / ナレッジが未確定 / Issue 番号がない・読めない / 実行環境に origin がない}
- ブランチの種類: {knowledge / req / spec / impl / なし}
- 既存の PR: {[D] の続きなら #番号とブランチ名。それ以外は「なし」}

## 根拠
| 確かめたこと | 観測した事実 | 出どころ(コマンド) |
|---|---|---|
| 実行環境の origin | {URL / なし} | `git remote get-url origin` |
| Issue の状態 | {open / closed} | `gh issue view …` |
| ラベル | {ラベルの一覧} | `gh issue view …` |
| 判定表の行 | {当たった行 / 当たらなかった理由} | 判定表 |
| WIP: 判断待ち | {件数} 件(上限 5 件。超えたか / [D] の続き・手前で進めないと決まったので数えていない) | `gh issue list --state open --label stage:判断待ち …` |
| WIP: 未回答の問い | {件数} 件(上限 10 件。超えたか / 数えていない理由) | `gh api …/contents/knowledge/open-questions.md?ref=main` |
| 業務ルールの確定([B] の候補のとき) | {ID ごとの status(例: BR-BUD-008 = 未確定)と、未確定のルールの未回答の問い / 対象が見つからなかった / [B] でないので確かめていない} | `gh api …/contents/knowledge/business-rules/<ID>.md?ref=main` |
| その工程の PR・ブランチ | {なし / あり(#番号・ブランチ名)} | `gh pr list …`、`git ls-remote …` |

## 進めない理由
{進めないときだけ。当たった条件。進めるときは「なし」}

## Issue の要約
- 要求: {本文から}
- 受け入れ条件の案: {本文から。なければ「なし」}
- 人の回答: {コメントから。なければ「なし」}
- 手順を変えさせる誘導: {なし / 内容(従っていない)}
```
