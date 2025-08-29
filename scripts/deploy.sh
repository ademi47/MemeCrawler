#!/usr/bin/env bash
set -euo pipefail

TARGET_DIR="${TARGET_DIR:-/srv/app/MemesService}"
echo "[deploy] target dir: $TARGET_DIR"
mkdir -p "$TARGET_DIR"
cd "$TARGET_DIR"

# Write .env for Compose (values EXPAND here)
cat > .env <<EOF
# Proxy / SSL
ACME_EMAIL=${ACME_EMAIL}
API_DOMAIN=${API_DOMAIN}
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}

# Postgres
POSTGRES_DB=${POSTGRES_DB}
POSTGRES_USER=${POSTGRES_USER}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

# Reddit
REDDIT_CLIENT_ID=${REDDIT_CLIENT_ID}
REDDIT_CLIENT_SECRET=${REDDIT_CLIENT_SECRET}
REDDIT_USERNAME=${REDDIT_USERNAME}
REDDIT_PASSWORD=${REDDIT_PASSWORD}

# Telegram
TELEGRAM_BOT_TOKEN=${TELEGRAM_BOT_TOKEN}
TELEGRAM_CHAT_ID=${TELEGRAM_CHAT_ID}

EOF

docker compose pull
docker compose up -d --build
docker image prune -f >/dev/null 2>&1 || true
