#!/usr/bin/env bash
# Humo directo de la cache de prompt contra el deployment gpt-5.6-luna. Envia dos veces la
# misma peticion (system de ~6.000 caracteres con prompt_cache_breakpoint explicito,
# prompt_cache_key y prompt_cache_options) separadas 3 s, e imprime para cada respuesta el
# codigo HTTP y los contadores prompt_tokens, cached_tokens y cache_write_tokens.
# Veredicto esperado: dos 200 y cached_tokens > 0 en la segunda respuesta.
# Solo lectura: no toca configuracion. Consume dos llamadas (unos 1.300 tokens de entrada cada una).
#
# Uso:
#   ./smoke-cache.sh                 # DEV, URL del deployment con api-version
#   URL_V1=1 ./smoke-cache.sh        # URL /openai/v1/chat/completions, "model" en el cuerpo, sin api-version
#   ACCOUNT_HOST=otra.openai.azure.com ./smoke-cache.sh
set -euo pipefail

ACCOUNT_HOST="${ACCOUNT_HOST:-srbaisrv01devdocai.openai.azure.com}"
DEPLOYMENT_NAME="${DEPLOYMENT_NAME:-gpt-5.6-luna}"
API_VERSION="${API_VERSION:-2025-04-01-preview}"
URL_V1="${URL_V1:-0}"

TOKEN=$(az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv)
[ -n "$TOKEN" ] || { echo "Sin token. Ejecuta az login." >&2; exit 2; }

if [ "$URL_V1" = "1" ]; then
  URL="https://${ACCOUNT_HOST}/openai/v1/chat/completions"
else
  URL="https://${ACCOUNT_HOST}/openai/deployments/${DEPLOYMENT_NAME}/chat/completions?api-version=${API_VERSION}"
fi

PARRAFO="Eres un clasificador documental de una entidad financiera. Recibes el contenido de un documento y debes asignarle una tipologia de una lista cerrada, justificando la eleccion con los datos que aparecen en el texto. Si el documento no encaja con claridad en ninguna tipologia, devuelves la tipologia Desconocido. No inventas datos que no esten en el documento. "
SYSTEM=""
for _ in $(seq 1 16); do SYSTEM="${SYSTEM}${PARRAFO}"; done
SYSTEM="${SYSTEM}Para esta prueba no clasifiques nada: responde unicamente con el JSON {\"ok\":true}."
HASH12=$(printf '%s' "$SYSTEM" | sha256sum | cut -c1-12)

BODY_FILE=$(mktemp)
RESPONSE_FILE=$(mktemp)
trap 'rm -f "$BODY_FILE" "$RESPONSE_FILE"' EXIT

SYSTEM_TEXT="$SYSTEM" HASH12="$HASH12" URL_V1="$URL_V1" DEPLOYMENT_NAME="$DEPLOYMENT_NAME" \
python -I - "$BODY_FILE" <<'PY'
import json, os, sys
body = {
    "messages": [
        {"role": "system", "content": [{"type": "text", "text": os.environ["SYSTEM_TEXT"],
                                         "prompt_cache_breakpoint": {"mode": "explicit"}}]},
        {"role": "user", "content": [{"type": "text", "text": 'Devuelve {"ok":true}'}]},
    ],
    "response_format": {"type": "json_object"},
    "reasoning_effort": "none",
    "max_completion_tokens": 60,
    "prompt_cache_key": "documentia-clasif:smoke:" + os.environ["HASH12"],
    "prompt_cache_options": {"mode": "implicit", "ttl": "30m"},
}
if os.environ["URL_V1"] == "1":
    body["model"] = os.environ["DEPLOYMENT_NAME"]
with open(sys.argv[1], "w", encoding="utf-8") as f:
    json.dump(body, f, ensure_ascii=False)
PY

echo "POST ${URL}"
echo "System: ${#SYSTEM} caracteres, prompt_cache_key documentia-clasif:smoke:${HASH12}"

for LLAMADA in 1 2; do
  [ "$LLAMADA" = "2" ] && sleep 3
  HTTP_CODE=$(curl -sS --ssl-no-revoke -o "$RESPONSE_FILE" -w '%{http_code}' \
    -X POST "$URL" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    --data-binary "@${BODY_FILE}")
  echo "--- Llamada ${LLAMADA}: HTTP ${HTTP_CODE}"
  python -I - "$RESPONSE_FILE" <<'PY' || true
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
usage = d.get("usage") or {}
det = usage.get("prompt_tokens_details") or {}
print("finish_reason:", choice.get("finish_reason"))
print("content:", (choice.get("message") or {}).get("content"))
print("prompt_tokens:", usage.get("prompt_tokens"))
print("cached_tokens:", det.get("cached_tokens"))
print("cache_write_tokens:", det.get("cache_write_tokens"))
PY
done
