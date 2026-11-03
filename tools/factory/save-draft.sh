#!/usr/bin/env bash
# takt が異常終了したときに、持ち場の中の書きかけをコミットしてブランチを push する(#101)。
# tools/factory/takt.sh が呼ぶ。AI は動けないことがある(利用上限)ので、決まった手順だけをする。
# 使い方: tools/factory/save-draft.sh <Issue番号> <ブランチ> <工程の名前> <止まった理由>
#   リポジトリの作業ツリーの直下で実行する。持ち場の外の変更はコミットせずに残し、一覧を出す。
#   終了コード: 0 = 残した(または変更がなかった)、1 = 残せなかった(ブランチが違う・push できない)
set -euo pipefail

[ $# -eq 4 ] || { sed -n '2,6p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 2; }
issue="$1" branch="$2" stage_name="$3" reason="$4"
cli="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/draft-cli.mjs"

say() { echo "▶ $*" >&2; }

[ "$(git branch --show-current)" = "$branch" ] || { echo "✖ いまのブランチが $branch でないので、書きかけを残さない。確かめてから実行する" >&2; exit 1; }
mapfile -d '' -t inside < <(node "$cli" files inside "$branch")
mapfile -d '' -t outside < <(node "$cli" files outside "$branch")

status=0
if [ ${#inside[@]} -gt 0 ]; then
  git add -A -- "${inside[@]}"
  git commit -q -m "[書きかけ] $stage_name: 異常終了($reason)" -m "takt が途中で止まった時点の下書き。次の tools/factory/takt.sh $issue が続きから再開する。" -m "Refs #$issue"
  if git push -q -u origin "$branch"; then
    say "書きかけ: ${#inside[@]} ファイルをコミットして $branch を push した。再開は tools/factory/takt.sh $issue"
  else
    echo "✖ 書きかけはコミットしたが、push できなかった。git push -u origin $branch を確かめてから再開する" >&2
    status=1
  fi
else
  say "書きかけ: 持ち場の中の変更はなかった"
fi
if [ ${#outside[@]} -gt 0 ]; then
  echo "✖ 持ち場の外の変更は、コミットせずに残した。人が確かめて片付けるまで、次の実行は止まる:" >&2
  printf '   %s\n' "${outside[@]}" >&2
fi
exit "$status"
