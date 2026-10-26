# 開発・ビルド・テスト共用イメージ(.NET 10 SDK + Node.js 22)
# VS Code Dev Containers と docker-compose.yml の両方から利用する。
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Node.js 22 と開発ツール
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg git jq sqlite3 sudo \
       iptables ipset iproute2 dnsutils aggregate \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# playwright は検証用の devDependency のため、既定ではブラウザをダウンロードしない。
# コンテナ内でブラウザ検証したい場合は `npx playwright install chromium` を実行する。
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1

# ホストとの UID/GID を揃えた非 root ユーザー(VS Code Dev Containers 推奨構成)
# ベースイメージ(Ubuntu 24.04)には UID/GID 1000 の ubuntu ユーザーが既に存在するため、
# 既存の UID/GID があればリネームして流用する(なければ新規作成)
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID
# sudo の範囲。full = 何でもできる(docker compose)。firewall-only = ファイアウォールと volume の持ち主の直しだけ
# (devcontainer。エージェントがファイアウォールを外せないようにする。ADR-0015 フェーズ7)
ARG SUDO_POLICY=full
RUN if getent group "$USER_GID" >/dev/null; then \
        groupmod --new-name "$USERNAME" "$(getent group "$USER_GID" | cut -d: -f1)"; \
    else \
        groupadd --gid "$USER_GID" "$USERNAME"; \
    fi \
    && if getent passwd "$USER_UID" >/dev/null; then \
        usermod --login "$USERNAME" --home "/home/$USERNAME" --move-home --shell /bin/bash \
            "$(getent passwd "$USER_UID" | cut -d: -f1)"; \
    else \
        useradd --uid "$USER_UID" --gid "$USER_GID" -m -s /bin/bash "$USERNAME"; \
    fi \
    && if [ "$SUDO_POLICY" = firewall-only ]; then \
        echo "$USERNAME ALL=(root) NOPASSWD: /usr/local/bin/init-firewall.sh, /usr/local/bin/devcontainer-fix-owner.sh" > /etc/sudoers.d/$USERNAME; \
    else \
        echo "$USERNAME ALL=(root) NOPASSWD:ALL" > /etc/sudoers.d/$USERNAME; \
    fi \
    && chmod 0440 /etc/sudoers.d/$USERNAME

# NuGet の named volume は /home/vscode/.nuget/packages にマウントする。
# マウント先の親ディレクトリ(.nuget)がイメージに無いと Docker が root 所有で自動生成してしまい、
# vscode ユーザーが NuGet.Config を書けず復元が失敗する。事前に vscode 所有で作成しておく
# (空の named volume は初回マウント時にこのマウント先ディレクトリの所有権を継承する)。
RUN mkdir -p /home/$USERNAME/.nuget/packages /home/$USERNAME/.claude \
    && chown -R $USER_UID:$USER_GID /home/$USERNAME/.nuget /home/$USERNAME/.claude

# devcontainer のファイアウォール(docker/init-firewall.sh)。root の持ち物にし、vscode ユーザーが書き換えられないようにする
COPY --chmod=0755 docker/init-firewall.sh docker/devcontainer-fix-owner.sh /usr/local/bin/

USER $USERNAME
WORKDIR /workspace

# dotnet の初回実行を軽くする
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
