#!/usr/bin/env bash
set -euo pipefail

echo "Starting local SQL Server container via docker compose..."
if [ ! -f .env ] && [ -f .env.example ]; then
  cp .env.example .env
fi

docker compose up -d db

echo "Waiting for database to become healthy..."
until [ "$(docker compose ps --format json db | grep -o '"Health":"healthy"' || true)" = '"Health":"healthy"' ]; do
  sleep 2
done

echo "SQL Server is healthy on port 1433."
