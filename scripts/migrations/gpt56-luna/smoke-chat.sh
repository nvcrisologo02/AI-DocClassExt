#!/usr/bin/env bash
# Humo directo contra el deployment gpt-5.6-luna: una llamada de chat con
# reasoning_effort y max_completion_tokens, con la api-version que usa el SDK
# Azure.AI.OpenAI 2.9.0-beta.1 (2025-04-01-preview). Confirma que el servicio acepta
# ambos campos con este modelo; el test unitario solo prueba el cuerpo que genera el SDK.
# Solo lectura: no toca configuracion. Consume una llamada (unos 30 tokens).
#
# Uso:
#   ./smoke-chat.sh                                   # DEV por defecto
#   ACCOUNT_HOST=otra.openai.azure.com ./smoke-chat.sh
#   REASONING_EFFORT=low MAX_COMPLETION_TOKENS=50 ./smoke-chat.sh
set -euo pipefail

ACCOUNT_HOST="${ACCOUNT_HOST:-srbaisrv01devdocai.openai.azure.com}"
DEPLOYMENT_NAME="${DEPLOYMENT_NAME:-gpt-5.6-luna}"
API_VERSION="${API_VERSION:-2025-04-01-preview}"
REASONING_EFFORT="${REASONING_EFFORT:-none}"
MAX_COMPLETION_TOKENS="${MAX_COMPLETION_TOKENS:-20}"

TOKEN=$(az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv)
[ -n "$TOKEN" ] || { echo "Sin token. Ejecuta az login." >&2; exit 2; }

URL="https://${ACCOUNT_HOST}/openai/deployments/${DEPLOYMENT_NAME}/chat/completions?api-version=${API_VERSION}"
BODY=$(printf '{"messages":[{"role":"user","content":"Responde solo OK"}],"reasoning_effort":"%s","max_completion_tokens":%s}' \
  "$REASONING_EFFORT" "$MAX_COMPLETION_TOKENS")

echo "POST ${URL}"
echo "Cuerpo: ${BODY}"

RESPONSE_FILE=$(mktemp)
trap 'rm -f "$RESPONSE_FILE"' EXIT
HTTP_CODE=$(curl -sS --ssl-no-revoke -o "$RESPONSE_FILE" -w '%{http_code}' \
  -X POST "$URL" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  --data "$BODY")

echo "HTTP ${HTTP_CODE}"
python -I - "$RESPONSE_FILE" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    raw = f.read()
try:
    d = json.loads(raw)
except json.JSONDecodeError:
    print("Respuesta no JSON:", raw[:500]); sys.exit(1)
if "error" in d:
    print("error:", json.dumps(d["error"], ensure_ascii=False)); sys.exit(1)
choice = (d.get("choices") or [{}])[0]
print("model:", d.get("model"))
print("finish_reason:", choice.get("finish_reason"))
print("content:", (choice.get("message") or {}).get("content"))
print("usage:", json.dumps(d.get("usage"), ensure_ascii=False))
PY
