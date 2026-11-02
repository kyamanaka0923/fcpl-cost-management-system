# 工場のゲートを破る操作の判定(ADR-0015)。エージェントの種類によらない、ガードの正本。
# 使う側: .claude/hooks/guard.sh(Claude Code のフック)、tools/factory/guard/bin/gh・git(どのエージェントでも効く包み)。
# source して使う。止めるときは理由を標準出力に出して 1 を返す。通すときは何も出さずに 0 を返す。

# $1 = シェルのコマンド文字列(フックはコマンド全体、包みは「gh 引数…」「git 引数…」)
factory_guard_command() {
  local norm push_segs
  # 改行・連続空白を1つの空白にそろえ、言い回しの揺れで素通りしないようにする
  norm="$(tr '\n\t' '  ' <<<"$1" | tr -s ' ')"

  if grep -Eq '(^|[^[:alnum:]_-])gh +pr +merge([^[:alnum:]_-]|$)' <<<"$norm"; then echo "PR のマージ(ゲート3)"; return 1; fi
  if grep -Eq '(^|[^[:alnum:]_-])gh +pr +ready([^[:alnum:]_-]|$)' <<<"$norm"; then echo "PR の Ready for review 化"; return 1; fi
  if grep -Eq '(^|[^[:alnum:]_-])gh +pr +review([^|;&]*)(--approve|-a( |$))' <<<"$norm"; then echo "PR の承認"; return 1; fi
  if grep -Eq -- '--(add|remove)-label[= ]+[^|;&]*gate[0-9]' <<<"$norm"; then echo "ゲートラベル(gate0〜2-approved)の付け外し"; return 1; fi
  if grep -Eq '(^|[^[:alnum:]_-])gh +label +(create|edit|delete|clone)([^[:alnum:]_-]|$)' <<<"$norm"; then echo "ラベルの定義の変更"; return 1; fi
  if grep -Eq 'update-approved-screens|--update-snapshots|playwright[^|;&]* -u( |$)' <<<"$norm"; then echo "承認した画面(requirements/screens/approved/)の撮り直し"; return 1; fi
  if grep -Eq '(^|[^[:alnum:]_-])gh +api([^[:alnum:]_-]|$)' <<<"$norm"; then
    # 読み取り(GET)は通し、書き込みだけを止める。-f/-F/--input があると gh api は POST になる
    if grep -Eq -- '(-X|--method)[= ]*(POST|PUT|PATCH|DELETE)|(^| )(-f|-F|--field|--raw-field|--input)( |=)' <<<"$norm"; then
      if grep -Eq '(rulesets|/protection)' <<<"$norm"; then echo "ルールセット・ブランチ保護の変更"; return 1; fi
      if grep -Eq '/labels' <<<"$norm"; then echo "API でのラベルの付け外し"; return 1; fi
      if grep -Eq '/merge([^[:alnum:]_-]|$)' <<<"$norm"; then echo "API での PR のマージ"; return 1; fi
    fi
  fi
  # git push の引数だけを見る。&& ; | でつないだ別のコマンド(gh pr create --base main など)の main や -f に反応しない
  push_segs="$(sed -E 's/(&&|\|\||;|\|)/\n/g' <<<"$norm" | grep -E '(^|[^[:alnum:]_-])git +push([^[:alnum:]_-]|$)' || true)"
  if [ -n "$push_segs" ]; then
    if grep -Eq -- '( |^)(--force|-f|--force-with-lease)([= ]|$)|( |:)\+' <<<"$push_segs"; then echo "強制 push"; return 1; fi
    if grep -Eq '(^|[ :])(refs/heads/)?main( |$)' <<<"$push_segs"; then echo "main への push"; return 1; fi
  fi
  return 0
}

# $1 = 書き換えるファイルのパス
factory_guard_path() {
  case "$1" in
    *requirements/screens/approved/*) echo "承認した画面(requirements/screens/approved/)の書き換え"; return 1 ;;
    *.factory/approvals/*) echo "承認記録(.factory/approvals/)の書き換え"; return 1 ;;
  esac
  return 0
}

factory_guard_explain() {
  echo "工場のルールで止めました: $1" >&2
  echo "これは人の仕事です(AGENTS.md「守ること」)。回避せず、人に頼んでください。" >&2
}
