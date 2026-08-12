#!/usr/bin/env bash
# create-dashboard.sh — Crea/actualiza las shared queries y el dashboard
# "Seguimiento DocumentIA" en sareb / AI DocClassExt. Idempotente.
# Requisitos: Azure CLI con sesion iniciada (az login), python 3 en PATH.
# Uso: bash scripts/ado/create-dashboard.sh
set -euo pipefail

ORG_URL="https://sareb.visualstudio.com"
PROJECT_ENC="AI%20DocClassExt"
ADO_RESOURCE="499b84ac-1321-427f-aa17-267ca6975798"
API_WIT="api-version=7.1"
API_DASH="api-version=7.1-preview.3"
DASHBOARD_NAME="Seguimiento DocumentIA"
FOLDER_NAME="Dashboard"
FOLDER_PATH="Shared%20Queries/Dashboard"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

# adorest METHOD URL [BODY_FILE] — llamada REST autenticada con token Entra.
adorest() {
  local method="$1" url="$2" body_file="${3:-}"
  if [ -n "$body_file" ]; then
    az rest --resource "$ADO_RESOURCE" --method "$method" --url "$url" \
      --headers "Content-Type=application/json" --body @"$body_file" -o json 2>/dev/null
  else
    az rest --resource "$ADO_RESOURCE" --method "$method" --url "$url" -o json 2>/dev/null
  fi
}

# jget RUTA — extrae un campo de un JSON leido por stdin.
jget() { python -c "import json,sys;d=json.load(sys.stdin);print(eval('d'+sys.argv[1]))" "$1"; }

echo "== Resolviendo proyecto y equipo por defecto =="
PROJECT_JSON=$(adorest GET "$ORG_URL/_apis/projects/$PROJECT_ENC?$API_WIT")
PROJECT_ID=$(echo "$PROJECT_JSON" | jget "['id']")
TEAM_ID=$(echo "$PROJECT_JSON" | jget "['defaultTeam']['id']")
TEAM_NAME=$(echo "$PROJECT_JSON" | jget "['defaultTeam']['name']")
echo "Proyecto: $PROJECT_ID"
echo "Equipo:   $TEAM_NAME ($TEAM_ID)"
