#!/usr/bin/env bash
# Crea los deployments gpt-5-mini (DataZoneStandard) en los recursos OpenAI de PRO.
# Idempotente: si el deployment ya existe, lo deja tal cual.
#
# Uso:
#   ./create-gpt5-deployments.sh              # solo cuenta principal (Sweden Central)
#   ./create-gpt5-deployments.sh --with-secondary   # tambien secundario (West Europe)
#   CAPACITY=100 ./create-gpt5-deployments.sh # override de capacidad (unidades de 1K TPM)
set -euo pipefail

SUBSCRIPTION="647c7246-54bc-4d31-b909-431cacf03272"   # Produccion Central
RG="SRBRGDOCSAIPROD"
PRIMARY_ACCOUNT="upe48-mm2avmdm-swedencentral"
SECONDARY_ACCOUNT="srbaisrv-westeurope"
DEPLOYMENT_NAME="gpt-5-mini"
MODEL_NAME="gpt-5-mini"
MODEL_VERSION="2025-08-07"
SKU_NAME="DataZoneStandard"          # equivalente de residencia EU al regional Standard actual
CAPACITY="${CAPACITY:-50}"           # 50 = 50K TPM; ajustar segun cuota disponible

WITH_SECONDARY=0
[ "${1:-}" = "--with-secondary" ] && WITH_SECONDARY=1

az account set --subscription "$SUBSCRIPTION"

create_deployment() {
  local account="$1"
  echo "== ${account}: deployment '${DEPLOYMENT_NAME}' =="

  if az cognitiveservices account deployment show \
       -g "$RG" -n "$account" --deployment-name "$DEPLOYMENT_NAME" -o none 2>/dev/null; then
    echo "   Ya existe. Sin cambios."
    az cognitiveservices account deployment show \
      -g "$RG" -n "$account" --deployment-name "$DEPLOYMENT_NAME" \
      --query "{model:properties.model.name,version:properties.model.version,sku:sku.name,capacity:sku.capacity}" -o table
    return 0
  fi

  # Verificar que el modelo/SKU esta disponible en la region de la cuenta
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
echo "  1. scripts/migrations/gpt5-mini/01-insert-gpt5-test-rows.sql  (filas de prueba A/B, inofensivo)"
echo "  2. Desplegar el fix de codigo (Temperature condicional) ANTES de activar"
echo "  3. scripts/migrations/gpt5-mini/02-activate-gpt5-models.sql   (cutover con backup)"
