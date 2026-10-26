#!/usr/bin/env bash
# guard.sh の確認。止めるべき操作が止まり、普段の操作が通ることを確かめる。
# 実行: bash .claude/hooks/guard.test.sh
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
fail=0

run() { # $1=期待(block|allow) $2=ツール名 $3=tool_input の JSON
  local json out code
  json="$(jq -nc --arg t "$2" --argjson i "$3" '{tool_name:$t, tool_input:$i}')"
  out="$("$here/guard.sh" <<<"$json" 2>&1)"; code=$?
  if { [ "$1" = block ] && [ $code -eq 2 ]; } || { [ "$1" = allow ] && [ $code -eq 0 ]; }; then
    echo "ok   $1  $2 $3"
  else
    echo "NG   $1  $2 $3 (終了コード $code: $out)"; fail=1
  fi
}
bash_() { run "$1" Bash "$(jq -nc --arg c "$2" '{command:$c}')"; }
file_() { run "$1" "$2" "$(jq -nc --arg p "$3" '{file_path:$p}')"; }

bash_ block 'gh pr merge 26 --squash'
bash_ block 'cd x && gh  pr   merge'
bash_ block 'gh pr ready 26'
bash_ block 'gh pr review 26 --approve'
bash_ block 'gh pr review 26 -a'
bash_ block 'gh issue edit 12 --add-label gate2-approved'
bash_ block 'gh pr edit 3 --remove-label "gate1-approved"'
bash_ block 'gh label create gate3-approved'
bash_ block 'gh api repos/o/r/rulesets -X POST --input r.json'
bash_ block 'gh api -X PUT repos/o/r/branches/main/protection --input p.json'
bash_ block 'gh api repos/o/r/issues/1/labels -f labels[]=gate0-approved'
bash_ block 'gh api -X PUT repos/o/r/pulls/2/merge'
bash_ block 'git push origin main'
bash_ block 'git push origin HEAD:main'
bash_ block 'git push --force origin impl/1-x'
bash_ block 'git push -f'
bash_ block 'git push origin +impl/1-x'

bash_ allow 'gh pr create --base main --draft'
bash_ allow 'gh pr review 26 --comment -b "指摘"'
bash_ allow 'gh pr checks 26'
bash_ allow 'gh issue edit 12 --add-label needs-answer'
bash_ allow 'gh label list'
bash_ allow 'gh api repos/o/r/issues/1/labels'
bash_ allow 'gh api repos/o/r/rulesets'
bash_ allow 'gh api repos/o/r/branches/main/protection'
bash_ allow 'git push -u origin impl/12-main-screen'
bash_ allow 'git push'
bash_ allow 'git log --oneline main..HEAD'

file_ block Write '/w/requirements/screens/approved/SCR-001-通常.png'
file_ block Edit '/w/.factory/approvals/ISSUE-1.yml'
file_ allow Edit '/w/requirements/screens/screens.yaml'
file_ allow Write '/w/backend/src/A.cs'

exit $fail
