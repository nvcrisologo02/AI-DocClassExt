#!/usr/bin/env bash
# Crea el deployment gpt-5.6-luna (DataZoneStandard) en el recurso Azure OpenAI del
# entorno indicado. Prueba en DEV de la candidata a sustituir gpt-4.1-mini (AB#99934,
# task AB#101370). Idempotente: si el deployment ya existe, lo deja tal cual.
#
# Uso:
#   ENV=dev ./create-gpt56-luna-deployment.sh                    # DEV (srbaisrv01devdocai)
#   ENV=pro ./create-gpt56-luna-deployment.sh                    # PRO, solo cuenta principal
#   ENV=pro ./create-gpt56-luna-deployment.sh --with-secondary   # PRO, tambien West Europe
#   ENV=dev CAPACITY=100 ./create-gpt56-luna-deployment.sh       # override de capacidad (1K TPM)
#
# ENV es obligatoria. 'pre' falla a proposito hasta que se rellene su bloque (TODO).
# En la unidad AB#99934 solo se ejecuta con ENV=dev; PRE y PRO son del cutover (AB#99939).
set -euo pipefail

ENV="${ENV:-}"
case "$ENV" in
  dev)
    SUBSCRIPTION="8764f9ff-fe37-4c03-bde9-6294622bef6d"   # Core Desarrollo
    RG="SRBRGDEVDOCSAI"
    PRIMARY_ACCOUNT="srbaisrv01devdocai"                   # West Europe
    SECONDARY_ACCOUNT=""
    ;;
  pre)
    # TODO: rellenar con la suscripcion Core Preproduccion (a4f6b357-...), el RG y la
    # cuenta OpenAI de PRE antes del cutover. Hasta entonces el script falla aqui.
    echo "ERROR: el bloque de PRE no esta definido en este script (TODO). Nada ejecutado." >&2
    exit 2
    ;;
  pro)
    SUBSCRIPTION="647c7246-54bc-4d31-b909-431cacf03272"   # Produccion Central
    RG="SRBRGDOCSAIPROD"
    PRIMARY_ACCOUNT="upe48-mm2avmdm-swedencentral"
    SECONDARY_ACCOUNT="srbaisrv-westeurope"
    ;;
  *)
    echo "ERROR: ENV debe ser dev, pre o pro (recibido: '${ENV}')." >&2
    exit 2
    ;;
esac

DEPLOYMENT_NAME="gpt-5.6-luna"
MODEL_NAME="gpt-5.6-luna"
MODEL_VERSION="2026-07-09"
SKU_NAME="DataZoneStandard"          # residencia EU; si no hay cuota, parar (no cambiar a Global)
CAPACITY="${CAPACITY:-50}"           # 50 = 50K TPM, igual que gpt-5-mini

WITH_SECONDARY=0
[ "${1:-}" = "--with-secondary" ] && WITH_SECONDARY=1
if [ "$WITH_SECONDARY" = "1" ] && [ -z "$SECONDARY_ACCOUNT" ]; then
  echo "AVISO: ${ENV} no tiene cuenta secundaria; se ignora --with-secondary."
  WITH_SECONDARY=0
fi

az account set --subscription "$SUBSCRIPTION"

create_deployment() {
  local account="$1"
  echo "== ${ENV} / ${account}: deployment '${DEPLOYMENT_NAME}' =="

  if az cognitiveservices account deployment show \
       -g "$RG" -n "$account" --deployment-name "$DEPLOYMENT_NAME" -o none 2>/dev/null; then
    echo "   Ya existe. Sin cambios."
    az cognitiveservices account deployment show \
      -g "$RG" -n "$account" --deployment-name "$DEPLOYMENT_NAME" \
      --query "{model:properties.model.name,version:properties.model.version,sku:sku.name,capacity:sku.capacity}" -o table
    return 0
  fi

  # Verificar que el modelo/version con ese SKU esta disponible en la region de la cuenta
  local location
  location=$(az cognitiveservices account show -g "$RG" -n "$account" --query location -o tsv)
  local available
  available=$(az cognitiveservices model list -l "$location" \
    --query "[?model.name=='${MODEL_NAME}' && model.version=='${MODEL_VERSION}'] | [0].model.skus[].name" -o tsv 2>/dev/null || true)
  if ! grep -q "$SKU_NAME" <<< "$available"; then
    echo "   ERROR: ${MODEL_NAME} ${MODEL_VERSION} con SKU ${SKU_NAME} no disponible en ${location}."
    echo "   SKUs disponibles: ${available:-ninguno}"
    return 1
  fi

  az cognitiveservices account deployment create \
    -g "$RG" -n "$account" \
    --deployment-name "$DEPLOYMENT_NAME" \
    --model-name "$MODEL_NAME" \
    --model-version "$MODEL_VERSION" \
    --model-format OpenAI \
    --sku-name "$SKU_NAME" \
    --sku-capacity "$CAPACITY"

  echo "   Creado: ${MODEL_NAME} ${MODEL_VERSION} / ${SKU_NAME} / ${CAPACITY}K TPM"
}

create_deployment "$PRIMARY_ACCOUNT"
if [ "$WITH_SECONDARY" = "1" ]; then
  create_deployment "$SECONDARY_ACCOUNT"
fi

echo
echo "Siguientes pasos (ver README.md):"
echo "  1. ./smoke-chat.sh                      (acepta reasoning_effort y max_completion_tokens?)"
echo "  2. 01-dev-insert-gpt56-luna-rows.sql    (filas classification/prompt.gpt56-luna, sin flags)"
echo "  3. 02-dev-tarifa-gpt56-luna.sql         (linea gpt-5.6-luna en tarifas.ia)"
