#!/usr/bin/env bash
# 工場(ソフトウェアファクトリー)の GitHub 側の設定を作る(ADR-0015)。
# ラベル・ルールセットの変更は「人の仕事」なので、エージェントからは実行できない(.claude/hooks/guard.sh が止める)。
# 人が手元の端末で実行する:
#   bash tools/factory/setup-github.sh            ラベルと main のルールセット
#   bash tools/factory/setup-github.sh --project  上に加えて GitHub Projects のボード
#                                                 (先に `gh auth refresh -s project` が必要)
# 何度実行してもよい(ラベルは上書き、ルールセットは同名があれば更新)。
set -euo pipefail

repo="${REPO:-kyamanaka0923/fcpl-cost-management-system}"
owner="${repo%%/*}"

label() { gh label create "$1" --repo "$repo" --color "$2" --description "$3" --force >/dev/null && echo "ラベル: $1"; }

# 工程(GitHub Projects の列と同じ。Projects を使わないときもラベルで状態が分かるようにする)
label "stage:準備中"   "C5DEF5" "上流([A]ナレッジ・[B]要件・画面)の下書き中"
label "stage:着手待ち" "0E8A16" "ゲート1(投入ゲート)を通った。AI は [C] 仕様に着手してよい"
label "stage:仕様確定" "1D76DB" "ゲート2を通った。AI は [D] 実装に着手してよい"
label "stage:実装中"   "FBCA04" "[D] 実装・[E] 検証の最中"
label "stage:判断待ち" "D93F0B" "人の判定待ち(ゲートの判定・問いへの回答)"
# ゲート(人だけが付ける)
label "gate0-approved" "5319E7" "ゲート0: ナレッジが出どころと一致している(人だけが付ける)"
label "gate1-approved" "5319E7" "ゲート1: 要件と画面を承認し、本ラインに投入した(人だけが付ける)"
label "gate2-approved" "5319E7" "ゲート2: 仕様・テスト計画を承認した。実装してよい(人だけが付ける)"
# 状態
label "needs-answer"   "B60205" "要件・仕様の穴。人の回答があるまで止める"
label "prep"           "BFDADC" "人の判定を楽にする下ごしらえ(判断待ちが上限を超えたとき)"
# 工場の変更(導入計画 §11.2 の種類)
label "factory"        "000000" "工場(指示・エージェント・CI・ナレッジの型)の変更"
label "factory:A"      "444444" "工場の変更 A: 軽微な改善(仕掛かり品はそのまま続行)"
label "factory:B"      "444444" "工場の変更 B: 検査の強化(警告で入れ、流れ切ってから必須)"
label "factory:C"      "444444" "工場の変更 C: 工程・成果物の形の変更(流し切るかやり直すかを決める)"
label "factory:D"      "444444" "工場の変更 D: ナレッジの訂正(マージ済みまで遡る)"
# 計測
label "intervention"   "E99695" "人の介入があった(原因を記録して工場を直す)"
label "factory-report" "C2E0C6" "工場の週報(factory-metrics.yml が毎週コメントを足す。閉じない)"

# main のルールセット。承認数は 0(判定者が 1 人で、作者は自分の PR を承認できないため)。
# 承認 = 人がマージすること。必須チェックは各ワークフローのジョブ名(name:)。
# まだ main で一度も動いていないジョブを必須にすると、そのジョブを足す PR 自身がマージできなくなる。
# そこで、main の最新コミットで実際に動いたジョブだけを必須にし、残りは表示だけする(ジョブを足した PR をマージしてから再実行する)。
wanted=(
  "バックエンド(ビルド・テスト)"
  "フロントエンド(静的解析・型チェック・ビルド)"
  "フロントエンド単独テスト(Vitest)"
  "ブラウザE2E(Playwright)"
  "工場のフック(ゲートを破る操作を止めるか)"
  "工場の検査スクリプトのテスト"
  "要件の書式(EARS)"
  "曖昧語の検査"
  "要件とシナリオの網羅"
  "ナレッジの出どころと確定"
  "画面要件と fixtures"
  "画面の差分(承認した画面との比較)"
  "工程の持ち場(role-guard)"
  "ゲートラベルの確認(gate-check)"
  "前提の版(upstream-approved)"
  "工場の版の更新(factory-version)"
  "作業項目を途中で閉じない(閉じるキーワード)"
  # 2 つのワークフローの判定のジョブ。名前が同じだと必須チェックで区別できないので、名前を分けている(#73)
  "変えた場所の判定(CI)"
  "変えた場所の判定(工場のゲート)"
)
ran_on_main="$(gh api --paginate "repos/$repo/commits/main/check-runs?per_page=100" --jq '.check_runs[].name')"
required=()
for c in "${wanted[@]}"; do
  if grep -Fxq "$c" <<<"$ran_on_main"; then
    required+=("$c")
  else
    echo "必須チェックに入れません(main でまだ動いていない): $c"
  fi
done
checks_json="$(printf '%s\n' "${required[@]}" | jq -R '{context: .}' | jq -s '.')"

ruleset_json="$(jq -n --argjson checks "$checks_json" '{
  name: "工場のゲート(main)",
  target: "branch",
  enforcement: "active",
  bypass_actors: [],
  conditions: { ref_name: { include: ["~DEFAULT_BRANCH"], exclude: [] } },
  rules: [
    { type: "deletion" },
    { type: "non_fast_forward" },
    { type: "pull_request",
      parameters: {
        required_approving_review_count: 0,
        dismiss_stale_reviews_on_push: false,
        require_code_owner_review: false,
        require_last_push_approval: false,
        required_review_thread_resolution: false
      } },
    { type: "required_status_checks",
      parameters: {
        strict_required_status_checks_policy: true,
        required_status_checks: $checks
      } }
  ]
}')"
existing="$(gh api "repos/$repo/rulesets" --jq '.[] | select(.name=="工場のゲート(main)") | .id')"
if [ -n "$existing" ]; then
  gh api -X PUT "repos/$repo/rulesets/$existing" --input - <<<"$ruleset_json" >/dev/null
  echo "ルールセットを更新しました(id: $existing、必須チェック ${#required[@]} 件)"
else
  gh api -X POST "repos/$repo/rulesets" --input - <<<"$ruleset_json" >/dev/null
  echo "ルールセットを作りました(必須チェック ${#required[@]} 件)"
fi

if [ "${1:-}" = "--project" ]; then
  # 列 = Status フィールドの選択肢。AGENTS.md の列と同じ並び
  number="$(gh project create --owner "$owner" --title "工場のライン(原価管理)" --format json --jq '.number')"
  gh project link "$number" --owner "$owner" --repo "$repo"
  field="$(gh project field-list "$number" --owner "$owner" --format json --jq '.fields[] | select(.name=="Status") | .id')"
  echo "Projects のボード #$number を作りました。"
  echo "Status($field)の選択肢を、画面で次の順に直してください: 準備中 / 着手待ち / 仕様確定 / 実装中 / 人の判断待ち / 完了"
fi
