#!/usr/bin/env bash
set -euo pipefail

cd "${TARGET_DIR}"

# Create/refresh .env from GitHub Secrets (keep only what we need)
cat > .env <<EOF
ACME_EMAIL=${ACME_EMAIL}
API_DOMAIN=${API_DOMAIN}
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
EOF

docker compose pull
docker compose up -d --build
docker image prune -f >/dev/null 2>&1 || true