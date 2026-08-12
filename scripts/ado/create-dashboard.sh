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

echo "== Carpeta Shared Queries/Dashboard =="
if ! adorest GET "$ORG_URL/$PROJECT_ENC/_apis/wit/queries/$FOLDER_PATH?$API_WIT" >/dev/null 2>&1; then
  printf '{"name":"%s","isFolder":true}' "$FOLDER_NAME" > "$TMP_DIR/folder.json"
  adorest POST "$ORG_URL/$PROJECT_ENC/_apis/wit/queries/Shared%20Queries?$API_WIT" "$TMP_DIR/folder.json" >/dev/null
  echo "Carpeta creada."
else
  echo "Carpeta ya existe."
fi

QUERY_IDS="$TMP_DIR/query_ids.txt"
: > "$QUERY_IDS"

# ensure_query NAME WIQL — crea la query o actualiza su WIQL si ya existe.
ensure_query() {
  local name="$1" wiql="$2" enc id
  enc=$(python -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$name")
  if id=$(adorest GET "$ORG_URL/$PROJECT_ENC/_apis/wit/queries/$FOLDER_PATH/$enc?$API_WIT" 2>/dev/null | jget "['id']" 2>/dev/null) && [ -n "$id" ]; then
    python -c "import json,sys;print(json.dumps({'wiql':sys.argv[1]}))" "$wiql" > "$TMP_DIR/q.json"
    adorest PATCH "$ORG_URL/$PROJECT_ENC/_apis/wit/queries/$FOLDER_PATH/$enc?$API_WIT" "$TMP_DIR/q.json" >/dev/null
    echo "Actualizada: $name"
  else
    python -c "import json,sys;print(json.dumps({'name':sys.argv[1],'wiql':sys.argv[2]}))" "$name" "$wiql" > "$TMP_DIR/q.json"
    id=$(adorest POST "$ORG_URL/$PROJECT_ENC/_apis/wit/queries/$FOLDER_PATH?$API_WIT" "$TMP_DIR/q.json" | jget "['id']")
    echo "Creada:      $name"
  fi
  echo "$name=$id" >> "$QUERY_IDS"
}

echo "== Shared queries =="
COLS="SELECT [System.Id], [System.WorkItemType], [System.Title], [System.State], [System.AssignedTo], [System.ChangedDate] FROM WorkItems WHERE [System.TeamProject] = @project"

ensure_query "KPI - Abiertos" \
  "$COLS AND [System.WorkItemType] IN ('Epic','Feature','Product Backlog Item','Task','Bug') AND [System.State] NOT IN ('Done','Removed') ORDER BY [System.ChangedDate] DESC"

ensure_query "KPI - Creados 7d" \
  "$COLS AND [System.WorkItemType] IN ('Epic','Feature','Product Backlog Item','Task','Bug') AND [System.CreatedDate] >= @Today - 7 ORDER BY [System.CreatedDate] DESC"

ensure_query "KPI - Cerrados 7d" \
  "$COLS AND [System.WorkItemType] IN ('Epic','Feature','Product Backlog Item','Task','Bug') AND [System.State] = 'Done' AND [Microsoft.VSTS.Common.ClosedDate] >= @Today - 7 ORDER BY [Microsoft.VSTS.Common.ClosedDate] DESC"

ensure_query "KPI - Bugs abiertos" \
  "$COLS AND [System.WorkItemType] = 'Bug' AND [System.State] NOT IN ('Done','Removed') ORDER BY [System.CreatedDate] DESC"

ensure_query "KPI - Pdte Despliegue" \
  "$COLS AND [System.State] = 'Pdte. Despliegue' ORDER BY [System.ChangedDate] DESC"

ensure_query "Features - Activas con hijos" \
  "SELECT [System.Id], [System.WorkItemType], [System.Title], [System.State], [System.AssignedTo] FROM WorkItemLinks WHERE (Source.[System.TeamProject] = @project AND Source.[System.WorkItemType] = 'Feature' AND Source.[System.State] IN ('New','In Progress')) AND ([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward') AND (Target.[System.State] <> 'Removed') ORDER BY [System.Id] MODE (Recursive)"

ensure_query "Chart - PBIs abiertos por estado" \
  "$COLS AND [System.WorkItemType] = 'Product Backlog Item' AND [System.State] NOT IN ('Done','Removed') ORDER BY [System.State]"

ensure_query "Chart - Tasks abiertas por estado" \
  "$COLS AND [System.WorkItemType] = 'Task' AND [System.State] NOT IN ('Done','Removed') ORDER BY [System.State]"

ensure_query "Chart - Bugs por estado" \
  "$COLS AND [System.WorkItemType] = 'Bug' AND [System.State] <> 'Removed' ORDER BY [System.State]"

ensure_query "Operativa - En curso" \
  "$COLS AND [System.State] IN ('In Progress','Committed') ORDER BY [System.ChangedDate] DESC"

ensure_query "Operativa - Estancados" \
  "$COLS AND [System.State] IN ('Approved','Committed','In Progress','To Validate','Pdte. Despliegue') AND [System.ChangedDate] < @Today - 14 ORDER BY [System.ChangedDate] ASC"

ensure_query "Operativa - Sin asignar" \
  "$COLS AND [System.WorkItemType] IN ('Product Backlog Item','Task','Bug') AND [System.State] NOT IN ('Done','Removed') AND [System.AssignedTo] = '' ORDER BY [System.CreatedDate] ASC"

echo "Queries registradas: $(wc -l < "$QUERY_IDS")"
