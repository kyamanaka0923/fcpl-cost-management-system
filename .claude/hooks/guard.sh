#!/usr/bin/env bash
# 工場のゲートを破る操作を止める PreToolUse フック(ADR-0015)。
# 入力: Claude Code が渡すツール呼び出しの JSON(標準入力)。
# 止めるときは理由を標準エラーに出して終了コード 2 を返す(Claude Code は呼び出しを取り消し、理由をエージェントに返す)。
set -euo pipefail

input="$(cat)"
tool="$(jq -r '.tool_name // ""' <<<"$input")"

block() {
  echo "工場のルールで止めました: $1" >&2
  echo "これは人の仕事です(AGENTS.md「守ること」)。回避せず、人に頼んでください。" >&2
  exit 2
}

case "$tool" in
  Bash)
    cmd="$(jq -r '.tool_input.command // ""' <<<"$input")"
    # 改行・連続空白を1つの空白にそろえ、言い回しの揺れで素通りしないようにする
    norm="$(tr '\n\t' '  ' <<<"$cmd" | tr -s ' ')"

    grep -Eq '(^|[^[:alnum:]_-])gh +pr +merge([^[:alnum:]_-]|$)' <<<"$norm" &&
      block "PR のマージ(ゲート3)"
    grep -Eq '(^|[^[:alnum:]_-])gh +pr +ready([^[:alnum:]_-]|$)' <<<"$norm" &&
      block "PR の Ready for review 化"
    grep -Eq '(^|[^[:alnum:]_-])gh +pr +review([^|;&]*)(--approve|-a( |$))' <<<"$norm" &&
      block "PR の承認"
    grep -Eq -- '--(add|remove)-label[= ]+[^|;&]*gate[0-9]' <<<"$norm" &&
      block "ゲートラベル(gate0〜2-approved)の付け外し"
    grep -Eq '(^|[^[:alnum:]_-])gh +label +(create|edit|delete|clone)([^[:alnum:]_-]|$)' <<<"$norm" &&
      block "ラベルの定義の変更"
    if grep -Eq '(^|[^[:alnum:]_-])gh +api([^[:alnum:]_-]|$)' <<<"$norm"; then
      # 読み取り(GET)は通し、書き込みだけを止める。-f/-F/--input があると gh api は POST になる
      if grep -Eq -- '(-X|--method)[= ]*(POST|PUT|PATCH|DELETE)|(^| )(-f|-F|--field|--raw-field|--input)( |=)' <<<"$norm"; then
        grep -Eq '(rulesets|/protection)' <<<"$norm" &&
          block "ルールセット・ブランチ保護の変更"
        grep -Eq '/labels' <<<"$norm" &&
          block "API でのラベルの付け外し"
        grep -Eq '/merge([^[:alnum:]_-]|$)' <<<"$norm" &&
          block "API での PR のマージ"
      fi
    fi
    # git push の引数だけを見る。&& ; | でつないだ別のコマンド(gh pr create --base main など)の main や -f に反応しない
    push_segs="$(sed -E 's/(&&|\|\||;|\|)/\n/g' <<<"$norm" | grep -E '(^|[^[:alnum:]_-])git +push([^[:alnum:]_-]|$)' || true)"
    if [ -n "$push_segs" ]; then
      grep -Eq -- '( |^)(--force|-f|--force-with-lease)([= ]|$)|( |:)\+' <<<"$push_segs" &&
        block "強制 push"
      grep -Eq '(^|[ :])(refs/heads/)?main( |$)' <<<"$push_segs" &&
        block "main への push"
    fi
    ;;
  Edit | Write | MultiEdit | NotebookEdit)
    path="$(jq -r '.tool_input.file_path // .tool_input.notebook_path // ""' <<<"$input")"
    case "$path" in
      *requirements/screens/approved/*) block "承認した画面(requirements/screens/approved/)の書き換え" ;;
      *.factory/approvals/*) block "承認記録(.factory/approvals/)の書き換え" ;;
    esac
    ;;
esac
exit 0
