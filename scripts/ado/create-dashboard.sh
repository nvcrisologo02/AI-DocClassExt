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
# Silencia los warnings TLS del proxy pero muestra el error real si la llamada falla.
adorest() {
  local method="$1" url="$2" body_file="${3:-}" rc=0 err="$TMP_DIR/adorest_stderr.txt"
  if [ -n "$body_file" ]; then
    az rest --resource "$ADO_RESOURCE" --method "$method" --url "$url" \
      --headers "Content-Type=application/json" --body @"$body_file" -o json 2>"$err" || rc=$?
  else
    az rest --resource "$ADO_RESOURCE" --method "$method" --url "$url" -o json 2>"$err" || rc=$?
  fi
  if [ "$rc" -ne 0 ]; then
    grep -vE "InsecureRequestWarning|urllib3|certificate verification" "$err" >&2 || true
    return "$rc"
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

echo "== Dashboard =="
python - "$QUERY_IDS" "$DASHBOARD_NAME" > "$TMP_DIR/dashboard.json" <<'PYEOF'
import json, sys

qids = dict(line.rstrip("\n").split("=", 1) for line in open(sys.argv[1], encoding="utf-8") if "=" in line)
name = sys.argv[2]

DASH = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards."
TILE = DASH + "QueryScalarWidget"
CHART = DASH + "WitChartWidget"
VIEW = "ms.vss-mywork-web.Microsoft.VisualStudioOnline.MyWork.WitViewWidget"

def w(nm, cid, r, c, rs, cs, settings):
    return {"name": nm, "contributionId": cid,
            "position": {"row": r, "column": c},
            "size": {"rowSpan": rs, "columnSpan": cs},
            "settings": settings,
            "settingsVersion": {"major": 1, "minor": 0, "patch": 0}}

def tile(nm, q, r, c):
    s = json.dumps({"queryId": qids[q], "queryName": q})
    return w(nm, TILE, r, c, 1, 1, s)

def chart(nm, q, r, c):
    s = json.dumps({
        "chartType": "pie", "scope": "WorkitemTracking.Queries", "groupKey": "System.State",
        "transformOptions": {"filter": qids[q], "groupBy": "System.State",
                             "orderBy": {"propertyName": "value", "direction": "descending"},
                             "measure": {"aggregation": "count", "propertyName": ""}},
        "userColors": [], "lastArtifactName": q})
    return w(nm, CHART, r, c, 2, 2, s)

def view(nm, q, r, c, rs, cs):
    s = json.dumps({"query": {"queryId": qids[q], "queryName": q},
                    "selectedColumns": [
                        {"referenceName": "System.Id", "name": "ID"},
                        {"referenceName": "System.WorkItemType", "name": "Work Item Type"},
                        {"referenceName": "System.Title", "name": "Title"},
                        {"referenceName": "System.State", "name": "State"},
                        {"referenceName": "System.AssignedTo", "name": "Assigned To"}]})
    return w(nm, VIEW, r, c, rs, cs, s)

def analytics(nm, cid, r, c, rs, cs):
    return w(nm, DASH + cid, r, c, rs, cs, None)

widgets = [
    tile("Abiertos", "KPI - Abiertos", 1, 1),
    tile("Creados 7d", "KPI - Creados 7d", 1, 2),
    tile("Cerrados 7d", "KPI - Cerrados 7d", 1, 3),
    tile("Bugs abiertos", "KPI - Bugs abiertos", 1, 4),
    tile("Pdte. Despliegue", "KPI - Pdte Despliegue", 1, 5),
    analytics("Burnup 30d", "BurnupWidget", 2, 1, 2, 3),
    analytics("Lead Time", "LeadTimeWidget", 2, 4, 2, 2),
    analytics("Cycle Time", "CycleTimeWidget", 2, 6, 2, 2),
    analytics("CFD", "CumulativeFlowDiagramWidget", 2, 8, 2, 2),
    view("Features activas", "Features - Activas con hijos", 4, 1, 2, 4),
    chart("PBIs abiertos por estado", "Chart - PBIs abiertos por estado", 4, 5),
    chart("Tasks abiertas por estado", "Chart - Tasks abiertas por estado", 4, 7),
    view("En curso", "Operativa - En curso", 6, 1, 2, 3),
    view("Estancados (>14d sin cambios)", "Operativa - Estancados", 6, 4, 2, 3),
    view("Sin asignar", "Operativa - Sin asignar", 6, 7, 2, 2),
    chart("Bugs por estado", "Chart - Bugs por estado", 6, 9),
]

print(json.dumps({"name": name,
                  "description": "Seguimiento de work items sin sprints: KPIs, tendencias, features y operativa. Gestionado por scripts/ado/create-dashboard.sh",
                  "refreshInterval": 0, "widgets": widgets}, ensure_ascii=False))
PYEOF

DASH_BASE="$ORG_URL/$PROJECT_ID/$TEAM_ID/_apis/dashboard/dashboards"
EXISTING_ID=$(adorest GET "$DASH_BASE?$API_DASH" | python -c "
import json,sys
d=json.load(sys.stdin)
print(next((x['id'] for x in d.get('value',[]) if x['name']==sys.argv[1]), ''))
" "$DASHBOARD_NAME")

if [ -n "$EXISTING_ID" ]; then
  # El PUT exige los eTags actuales (dashboard y widgets). Ademas, se conservan
  # los settings ya configurados de los widgets Analytics (config manual one-time)
  # casando por nombre, para que re-ejecutar no los borre.
  adorest GET "$DASH_BASE/$EXISTING_ID?$API_DASH" > "$TMP_DIR/current.json"
  python - "$TMP_DIR/dashboard.json" "$TMP_DIR/current.json" <<'PYEOF2'
import json, sys
new = json.load(open(sys.argv[1], encoding="utf-8"))
cur = json.load(open(sys.argv[2], encoding="utf-8"))
new["id"] = cur["id"]
if cur.get("eTag") is not None:
    new["eTag"] = cur["eTag"]
ANALYTICS = ("BurnupWidget", "LeadTimeWidget", "CycleTimeWidget", "CumulativeFlowDiagramWidget")
by_name = {w.get("name"): w for w in cur.get("widgets", [])}
for w in new["widgets"]:
    old = by_name.get(w["name"])
    if not old:
        continue
    w["id"] = old["id"]
    if old.get("eTag") is not None:
        w["eTag"] = old["eTag"]
    if w["contributionId"].endswith(ANALYTICS) and old.get("settings"):
        w["settings"] = old["settings"]
        w["settingsVersion"] = old.get("settingsVersion")
json.dump(new, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False)
PYEOF2
  adorest PUT "$DASH_BASE/$EXISTING_ID?$API_DASH" "$TMP_DIR/dashboard.json" >/dev/null
  echo "Dashboard actualizado: $EXISTING_ID"
  DASH_ID="$EXISTING_ID"
else
  DASH_ID=$(adorest POST "$DASH_BASE?$API_DASH" "$TMP_DIR/dashboard.json" | jget "['id']")
  echo "Dashboard creado: $DASH_ID"
fi

echo ""
echo "URL: $ORG_URL/AI%20DocClassExt/_dashboards/dashboard/$DASH_ID"
