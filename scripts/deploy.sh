#!/usr/bin/env bash
set -euo pipefail

cd "${TARGET_DIR}"

cat > .env <<EOF
# Proxy / SSL
ACME_EMAIL=${ACME_EMAIL}
API_DOMAIN=${API_DOMAIN}

# App
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}

# Postgres
POSTGRES_DB=${POSTGRES_DB}
POSTGRES_USER=${POSTGRES_USER}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

cat > ${TARGET_DIR}/src/Memes.Api/appsettings.Production.json <<EOF
{
  "Reddit": {
    "ClientId": "${REDDIT_CLIENT_ID}",
    "ClientSecret": "${REDDIT_CLIENT_SECRET}",
    "Username": "${REDDIT_USERNAME}",
    "Password": "${REDDIT_PASSWORD}",
    "UserAgent": "MemeCrawler/1.0 (by u:${REDDIT_USERNAME})"
  }
}


EOF

docker compose pull
docker compose up -d --build
docker image prune -f >/dev/null 2>&1 || true