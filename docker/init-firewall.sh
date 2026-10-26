#!/bin/bash
# devcontainer の外への通信を、許可した宛先だけに絞る(ADR-0015 フェーズ7、導入計画 §16.4)。
# Anthropic の Claude Code 参考実装(.devcontainer/init-firewall.sh)を土台にしている。
# devcontainer の postStartCommand が root で実行する(vscode ユーザーの sudo はこのスクリプトだけ)。
# 宛先を足すときは ALLOWED_DOMAINS に足し、devcontainer を作り直す(工場の変更として PR で)。
set -euo pipefail
IFS=$'\n\t'

# 途中で失敗したら、開いたままにせず外への通信をすべて止める(fail closed)
fail_closed() {
  echo "エラー: ファイアウォールの設定に失敗したので、外への通信をすべて止めました(ループバックだけ通す)" >&2
  iptables -P INPUT DROP || true
  iptables -P FORWARD DROP || true
  iptables -P OUTPUT DROP || true
  iptables -F OUTPUT || true
  iptables -A OUTPUT -o lo -j ACCEPT || true
}
# exit 1 で止めたときも含めて、終了コードが 0 でなければ閉じる
trap 'rc=$?; [ "$rc" -ne 0 ] && fail_closed; exit "$rc"' EXIT

ALLOWED_DOMAINS=(
  # Claude Code
  api.anthropic.com
  statsig.com
  sentry.io
  # npm
  registry.npmjs.org
  # NuGet
  api.nuget.org
  globalcdn.nuget.org
  # Playwright のブラウザ
  cdn.playwright.dev
  playwright.download.prss.microsoft.com
  # GitHub(IP の範囲は api.github.com/meta から取る。ここは meta に入らないもの)
  objects.githubusercontent.com
  raw.githubusercontent.com
  codeload.github.com
  # VS Code の拡張機能
  marketplace.visualstudio.com
  vscode.blob.core.windows.net
  update.code.visualstudio.com
)

# Docker の内部 DNS(127.0.0.11)の NAT 規則は、消す前に取っておいて戻す
DOCKER_DNS_RULES=$(iptables-save -t nat | grep "127\.0\.0\.11" || true)

iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

if [ -n "$DOCKER_DNS_RULES" ]; then
  iptables -t nat -N DOCKER_OUTPUT 2>/dev/null || true
  iptables -t nat -N DOCKER_POSTROUTING 2>/dev/null || true
  echo "$DOCKER_DNS_RULES" | xargs -L 1 iptables -t nat
fi

# 許可リストを作る前に要る通信: DNS・SSH・ループバック
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
iptables -A INPUT -p udp --sport 53 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 53 -j ACCEPT
iptables -A INPUT -p tcp --sport 53 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 22 -j ACCEPT
iptables -A INPUT -p tcp --sport 22 -m state --state ESTABLISHED -j ACCEPT
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT

ipset create allowed-domains hash:net

echo "GitHub の IP の範囲を取得しています"
gh_ranges=$(curl -fsS --max-time 20 https://api.github.com/meta)
if ! echo "$gh_ranges" | jq -e '.web and .api and .git' >/dev/null; then
  echo "エラー: api.github.com/meta の応答に web・api・git がありません" >&2
  exit 1
fi
while read -r cidr; do
  if [[ ! "$cidr" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$ ]]; then
    echo "エラー: GitHub の範囲の形が違います: $cidr" >&2
    exit 1
  fi
  ipset add allowed-domains "$cidr" -exist
done < <(echo "$gh_ranges" | jq -r '(.web + .api + .git)[]' | grep -v ':' | aggregate -q)

for domain in "${ALLOWED_DOMAINS[@]}"; do
  ips=$(dig +short A "$domain" | grep -E '^[0-9]{1,3}(\.[0-9]{1,3}){3}$' || true)
  if [ -z "$ips" ]; then
    # ドメインがなくなっただけなら、ほかの宛先は使えるようにする(止めるのはこのドメインだけ)
    echo "警告: $domain の IP を引けないので、許可リストに入れません" >&2
    continue
  fi
  while read -r ip; do
    ipset add allowed-domains "$ip" -exist
  done <<<"$ips"
done

# ホスト(ポートの転送・VS Code との通信)は通す
HOST_IP=$(ip route | awk '/default/ {print $3; exit}')
if [ -z "$HOST_IP" ]; then
  echo "エラー: ホストの IP が分かりません" >&2
  exit 1
fi
HOST_NETWORK=$(echo "$HOST_IP" | sed 's/\.[0-9]*$/.0\/24/')
iptables -A INPUT -s "$HOST_NETWORK" -j ACCEPT
iptables -A OUTPUT -d "$HOST_NETWORK" -j ACCEPT

# 既定はすべて捨て、許可リストと確立済みの通信だけを通す
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m set --match-set allowed-domains dst -j ACCEPT
# 許可リストにない宛先は、待たせずにすぐ拒否する(原因が分かるように)
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

# IPv6 はループバック以外すべて止める(許可リストは IPv4 だけなので、IPv6 で抜けられないように)
if command -v ip6tables >/dev/null && ip6tables -L >/dev/null 2>&1; then
  ip6tables -F
  ip6tables -A INPUT -i lo -j ACCEPT
  ip6tables -A OUTPUT -o lo -j ACCEPT
  ip6tables -P INPUT DROP
  ip6tables -P FORWARD DROP
  ip6tables -P OUTPUT DROP
fi

echo "確かめています"
if curl -fsS --max-time 5 https://example.com >/dev/null 2>&1; then
  echo "エラー: 許可していない example.com に届きました" >&2
  exit 1
fi
if ! curl -fsS --max-time 5 https://api.github.com/zen >/dev/null 2>&1; then
  echo "エラー: 許可した api.github.com に届きません" >&2
  exit 1
fi
echo "ファイアウォールを設定しました(許可: GitHub ほか ${#ALLOWED_DOMAINS[@]} ドメイン)"
