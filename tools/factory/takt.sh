#!/usr/bin/env bash
# 工場のライン(takt)で、1 つの Issue の工程を 1 つだけ進める。ゲートのガードを効かせて動かす。
# 使い方: tools/factory/takt.sh <Issue番号> [knowledge|req|spec|impl] [claude|codex|copilot] [-- takt に渡す引数…]
#   工程を省くと、Issue のラベルから選ぶ(tools/factory/lib/gate-labels.mjs の selectStage)。
#   エージェントを省くと、環境変数 FACTORY_AGENT、それもなければ claude。
#   例: tools/factory/takt.sh 45          tools/factory/takt.sh 45 spec codex
# 同じコマンドをもう一度実行すると、既存のブランチ・PR の「続き」になる(CI の失敗・PR の指摘を直す。判断待ちで止めた書きかけから再開する)。
#
# ここでするのは、決まった手順だけ(AI に判断させない):
#   1. ラベルから工程を選ぶ・確かめる   2. ブランチを用意する(<種類>/<番号>-takt。続きは既存の <種類>/<番号>-* に origin/main を取り込む)
#   3. 回答のあとなら列を stage:判断待ち から戻す。[D] の着手なら列を stage:仕様確定 → stage:実装中 にする   4. ガードを PATH の先頭に置いて takt を動かす
#   5. takt が異常終了したら(利用上限・エラー・Ctrl+C)、持ち場の中の書きかけをコミットしてブランチを push する(#101)
set -euo pipefail

usage() {
  sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

[ $# -ge 1 ] || usage
issue="${1#\#}"
shift
[[ "$issue" =~ ^[0-9]+$ ]] || usage

role=""
agent="${FACTORY_AGENT:-claude}"
while [ $# -gt 0 ]; do
  case "$1" in
    knowledge | req | spec | impl) role="$1" ;;
    claude | codex | copilot) agent="$1" ;;
    --) shift; break ;;
    *) echo "工程(knowledge・req・spec・impl)かエージェント(claude・codex・copilot)ではない: $1" >&2; exit 2 ;;
  esac
  shift
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
export PATH="$root/tools/factory/guard/bin:$PATH"
export FACTORY_AGENT="$agent"

say() { echo "▶ $*" >&2; }
fail() { echo "✖ $*" >&2; exit 1; }

# 0. ガードと作業ツリー
bash tools/factory/guard/check-active.sh >/dev/null || { bash tools/factory/guard/check-active.sh >&2; exit 1; }
git remote get-url origin >/dev/null 2>&1 || fail "origin がない。リポジトリの checkout で実行する(takt の worktree は origin を外す)"
[ -z "$(git status --porcelain)" ] || fail "作業ツリーに未コミットの変更がある。他の変更を混ぜないため、片付けてから実行する"

# 1. 工程を選ぶ・確かめる
picked="$(node tools/factory/line-cli.mjs "$issue" ${role:+"$role"})" || exit 1
eval "$picked"
say "Issue #$issue: $STAGE_NAME を進める(workflow $WORKFLOW、エージェント $agent)${REDO:+。redo:$ROLE による上流のやり直し}"

# 2. ブランチを用意する
git fetch -q origin
mapfile -t remote < <(git ls-remote --heads origin "refs/heads/$ROLE/$issue-*" | awk '{sub("refs/heads/", "", $2); print $2}')
[ ${#remote[@]} -le 1 ] || fail "$ROLE/$issue-* のブランチが ${#remote[@]} つある(${remote[*]})。1 つにしてから実行する"
if [ ${#remote[@]} -eq 1 ]; then
  branch="${remote[0]}"
  if git show-ref -q --verify "refs/heads/$branch"; then
    git switch -q "$branch"
    git merge -q --ff-only "origin/$branch" || fail "$branch を origin にそろえられない(早送りできない)"
  else
    git switch -q -c "$branch" --track "origin/$branch"
  fi
  say "続き: 既存のブランチ $branch"
  # 判断待ちの回答で上流(要件・仕様)が変わっていることがあるので、main の最新を取り込む(#98)
  if ! git merge-base --is-ancestor origin/main HEAD; then
    git merge -q --no-edit -m "origin/main を取り込む(工場の続きの起点をそろえる)" origin/main \
      || { git merge --abort; fail "$branch に origin/main を取り込むと衝突する。人が解いてから実行する"; }
    say "続き: origin/main を取り込んだ"
  fi
else
  branch="$ROLE/$issue-takt"
  if git show-ref -q --verify "refs/heads/$branch"; then
    # 前の実行で作って push しなかったブランチ。コミットがなければ origin/main から作り直す
    [ "$(git rev-list --count "origin/main..$branch")" = 0 ] || fail "$branch に push していないコミットがある。確かめてから実行する"
    git switch -q -C "$branch" origin/main
  else
    git switch -q -c "$branch" origin/main
  fi
  say "着手: $branch を origin/main から作った"
fi
pr="$(gh pr list --head "$branch" --state open --json number --jq '.[0].number // empty')"
drafts="$(git rev-list --count --no-merges origin/main..HEAD)"

# 3. 列を付け替える(外すものと付けるものを1つのコマンドで)
# 人が回答して needs-answer を外したら、stage:判断待ち を escalate が記録した列に戻す(#102)
if [ -n "$RESTORE_STAGE" ]; then
  gh issue edit "$issue" --remove-label "stage:判断待ち" --add-label "$RESTORE_STAGE" >/dev/null
  say "列: stage:判断待ち → $RESTORE_STAGE(回答のあとの戻し)"
fi
# [D] の着手なら stage:仕様確定 → stage:実装中
if [ "$ROLE" = impl ] && [ "$STAGE_LABEL" = "stage:仕様確定" ]; then
  gh issue edit "$issue" --remove-label "stage:仕様確定" --add-label "stage:実装中" >/dev/null
  say "列: stage:仕様確定 → stage:実装中"
fi

# 4. takt を動かす。タスクの本文が、書く step・publish・escalate への引き継ぎになる
task="#$issue
- 工程: $STAGE_NAME($ROLE)
- ブランチ: $branch
- 既存の PR: ${pr:+#$pr}${pr:-なし}
- 前の実行のコミット: ${drafts} 件
- 上流のやり直し: ${REDO:+あり(Issue の redo:$ROLE。回答で決まったことを直す)}${REDO:-なし}
- エージェント: $agent"
# --skip-git: ブランチ・コミット・PR は workflow が自分で扱う。takt の worktree は origin を外すので使わない
# exec にしない: takt の終わり方を見て、異常終了なら書きかけを残す(5.)
started="$(node -e 'console.log(Date.now())')"
interrupted=""
# Ctrl+C は takt にも届く。ここでは無視せず印だけ付け(無視にすると takt にも引き継がれる)、takt が終わるのを待つ
trap 'interrupted="Ctrl+C で中断"' INT
trap 'interrupted="SIGTERM で中断"' TERM
set +e
takt --pipeline --skip-git -w "$WORKFLOW" --runtime-assignment "$agent" -t "$task" "$@"
rc=$?
set -e
trap - INT TERM

# 5. 異常終了なら、書きかけを残す。AI は動けないことがある(利用上限)ので、スクリプトの決まった手順でする
IFS=$'\t' read -r run_status run_reason < <(FACTORY_TASK="$task" node tools/factory/draft-cli.mjs run "$started")
if [ "$rc" -eq 0 ] && [ "$run_status" = completed ] && [ -z "$interrupted" ]; then
  exit 0
fi
reason="${interrupted:-${run_reason:-takt の終了コード $rc、run の状態 $run_status}}"
echo "✖ takt が異常終了した: $reason" >&2
bash tools/factory/save-draft.sh "$issue" "$branch" "$STAGE_NAME" "$reason"
[ "$rc" -ne 0 ] && exit "$rc"
exit 1
