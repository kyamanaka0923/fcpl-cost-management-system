# 開発・ビルド・テスト共用イメージ(.NET 10 SDK + Node.js 22)
# VS Code Dev Containers と docker-compose.yml の両方から利用する。
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Node.js 22 と開発ツール
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg git sqlite3 sudo \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# playwright は検証用の devDependency のため、既定ではブラウザをダウンロードしない。
# コンテナ内でブラウザ検証したい場合は `npx playwright install chromium` を実行する。
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1

# ホストとの UID/GID を揃えた非 root ユーザー(VS Code Dev Containers 推奨構成)
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID
RUN groupadd --gid $USER_GID $USERNAME \
    && useradd --uid $USER_UID --gid $USER_GID -m -s /bin/bash $USERNAME \
    && echo "$USERNAME ALL=(root) NOPASSWD:ALL" > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME

USER $USERNAME
WORKDIR /workspace

# dotnet の初回実行を軽くする
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
