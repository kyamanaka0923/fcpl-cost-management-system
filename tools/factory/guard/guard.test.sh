#!/usr/bin/env bash
# gh・git の包み(bin/)と check-active.sh の確認。止めるべき操作が止まり、普段の操作が本物に渡ることを確かめる。
# 判定そのもの(rules.sh)の網羅は .claude/hooks/guard.test.sh が確かめる。ここは包みとしての振る舞いを見る。
# 実行: bash tools/factory/guard/guard.test.sh
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
fail=0

# 本物の代わり: 呼ばれたら「real <名前> <引数>」と出す
fake="$(mktemp -d)"
trap 'rm -rf "$fake"' EXIT
for name in gh git; do
  printf '#!/usr/bin/env bash\necho "real %s $*"\n' "$name" >"$fake/$name"
  chmod +x "$fake/$name"
done
guarded_path="$here/bin:$fake:/usr/bin:/bin"

run() { # $1=期待(block|allow) $2.. = コマンドと引数
  local expect="$1" out code
  shift
  out="$(PATH="$guarded_path" "$@" 2>&1)"; code=$?
  if [ "$expect" = block ] && [ $code -eq 2 ] && grep -q "工場のルールで止めました" <<<"$out"; then
    echo "ok   block $*"
  elif [ "$expect" = allow ] && [ $code -eq 0 ] && [ "$out" = "real $*" ]; then
    echo "ok   allow $*"
  else
    echo "NG   $expect $* (終了コード $code: $out)"; fail=1
  fi
}

run block gh pr merge 26 --squash
run block gh pr ready 26
run block gh pr review 26 --approve
run block gh issue edit 12 --add-label gate2-approved
run block gh label create gate3-approved
run block gh api -X PUT repos/o/r/pulls/2/merge
run block git push origin main
run block git push --force origin impl/1-x
run block git push origin +impl/1-x

run allow gh pr create --base main --draft
run allow gh issue edit 12 --add-label needs-answer
run allow gh pr checks 26
run allow git push -u origin impl/12-main-screen
run allow git log --oneline main..HEAD
run allow git switch -c spec/1-x origin/main

# check-active.sh: 包みが PATH の先頭にあるときだけ通る
if PATH="$guarded_path" bash "$here/check-active.sh" >/dev/null; then
  echo "ok   check-active: 包みが先頭なら通る"
else
  echo "NG   check-active: 包みが先頭なのに落ちた"; fail=1
fi
if PATH="$fake:$here/bin:/usr/bin:/bin" bash "$here/check-active.sh" >/dev/null; then
  echo "NG   check-active: 本物が先頭なのに通った"; fail=1
else
  echo "ok   check-active: 本物が先頭なら落ちる"
fi

exit $fail
