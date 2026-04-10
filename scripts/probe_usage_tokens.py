"""
Sonda de respuesta raw para Azure Content Understanding (CU) y Azure Document Intelligence (DI).

Objetivo:  Volcar el JSON crudo de respuesta de cada servicio y resumir
           los bloques usage / pages / tokens para determinar qué campos
           devuelve realmente cada servicio y si incluye conteo de tokens.

Uso:
  # Solo CU (extracción):
  python probe_usage_tokens.py --file sample.pdf ^
      --cu-endpoint https://xxx.services.ai.azure.com ^
      --cu-key KEY ^
      --cu-analyzer CU_NS_1.4_2

  # Solo DI (clasificación):
  python probe_usage_tokens.py --file sample.pdf ^
      --di-endpoint https://xxx.cognitiveservices.azure.com ^
      --di-key KEY ^
      --di-classifier sareb-classifier-v1

  # Ambos a la vez:
  python probe_usage_tokens.py --file sample.pdf ^
      --cu-endpoint ... --cu-key ... --cu-analyzer ... ^
      --di-endpoint ... --di-key ... --di-classifier ...

Resultado:
  Ficheros JSON volcados junto al documento de entrada:
    cu_probe_response.json
    di_probe_response.json
  Resumen en consola de todos los campos con "usage", "page" o "token".

Requiere:
  pip install requests
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import time
from typing import Any

try:
    import requests
    from requests import Response
except ImportError:
    print("ERROR: instala dependencias con:  pip install requests")
    sys.exit(1)


# ─────────────────────────────────────────────────────────────────────────────
#  Constantes de API
# ─────────────────────────────────────────────────────────────────────────────

CU_API_VERSION = "2025-11-01"
DI_API_VERSION = "2024-11-30"

_CONTENT_TYPES: dict[str, str] = {
    ".pdf":  "application/pdf",
    ".png":  "image/png",
    ".jpg":  "image/jpeg",
    ".jpeg": "image/jpeg",
    ".tif":  "image/tiff",
    ".tiff": "image/tiff",
}


# ─────────────────────────────────────────────────────────────────────────────
#  Helpers
# ─────────────────────────────────────────────────────────────────────────────

def content_type_for(path: str) -> str:
    return _CONTENT_TYPES.get(os.path.splitext(path)[1].lower(), "application/octet-stream")


def save_json(data: dict, out_path: str, label: str) -> None:
    pretty = json.dumps(data, indent=2, ensure_ascii=False)
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(pretty)
    print(f"\n{'=' * 72}")
    print(f"  {label}  →  {out_path}")
    print("=" * 72)
    if len(pretty) <= 6000:
        print(pretty)
    else:
        print(pretty[:6000])
        print(f"\n  ... (respuesta completa en {out_path})")


def summarize_interesting(label: str, data: dict) -> None:
    """Extrae recursivamente todos los escalares cuya ruta o clave sea
    relevante para usage / pages / tokens y los imprime en consola."""
    print(f"\n{'─' * 72}")
    print(f"  RESUMEN  usage / pages / tokens  ─  {label}")
    print("─" * 72)

    hits: list[tuple[str, Any]] = []
    _collect_scalars(data, path="", out=hits)

    keywords = ("usage", "page", "token", "count", "billing")
    relevant = [
        (path, val)
        for path, val in hits
        if any(kw in path.lower() for kw in keywords)
    ]

    if relevant:
        for path, val in relevant:
            print(f"  {path}  =  {val!r}")
    else:
        print("  (no se encontraron campos relacionados con usage / pages / tokens)")

    # Destaca explícitamente `usage` si existe en la raíz
    usage = data.get("usage") or (
        data.get("analyzeResult", {}).get("usage") if isinstance(data.get("analyzeResult"), dict) else None
    )
    if usage:
        print(f"\n  [usage block]\n  {json.dumps(usage, indent=4)}")

    # Destaca también el bloque result.usage de CU si existe
    cu_result = data.get("result") if isinstance(data.get("result"), dict) else {}
    if cu_result.get("usage"):
        print(f"\n  [result.usage block (CU)]\n  {json.dumps(cu_result['usage'], indent=4)}")


def _collect_scalars(obj: Any, path: str, out: list[tuple[str, Any]]) -> None:
    """Recorre el JSON en profundidad limitando arrays a los primeros 5 elementos."""
    if isinstance(obj, dict):
        for key, val in obj.items():
            new_path = f"{path}.{key}" if path else key
            if isinstance(val, (str, int, float, bool, type(None))):
                out.append((new_path, val))
            else:
                _collect_scalars(val, new_path, out)
    elif isinstance(obj, list):
        for i, item in enumerate(obj[:5]):
            _collect_scalars(item, f"{path}[{i}]", out)


def _poll_operation(op_url: str, api_key: str, label: str, timeout_s: int = 120) -> dict:
    """Espera de polling estándar Azure: status succeeded / failed / canceled."""
    deadline = time.monotonic() + timeout_s
    headers = {"Ocp-Apim-Subscription-Key": api_key}
    print(f"  [{label}] Polling operation-location…")

    while time.monotonic() < deadline:
        time.sleep(2)
        resp = requests.get(op_url, headers=headers, timeout=30)
        if not resp.ok:
            _abort(label, resp)

        data = resp.json()
        status: str = (
            data.get("status")
            or data.get("analyzeStatus")
            or data.get("analyzingStatus")
            or ""
        ).lower()
        print(f"  [{label}] status = {status}")

        if status in ("succeeded", "completed"):
            return data
        if status in ("failed", "canceled"):
            print(f"  [{label}] Operación terminó con estado '{status}'")
            print(json.dumps(data, indent=2)[:1500])
            sys.exit(1)

    print(f"\n[{label}] Timeout después de {timeout_s}s esperando resultado.")
    sys.exit(1)


def _abort(label: str, resp: Response) -> None:
    print(f"\n[{label}] Error HTTP {resp.status_code}:\n{resp.text[:600]}")
    sys.exit(1)


# ─────────────────────────────────────────────────────────────────────────────
#  Content Understanding  (CU)
# ─────────────────────────────────────────────────────────────────────────────

def probe_cu(endpoint: str, api_key: str, analyzer_id: str,
             file_bytes: bytes, content_type: str) -> dict:
    endpoint = endpoint.rstrip("/")
    url = (
        f"{endpoint}/contentunderstanding/analyzers/"
        f"{analyzer_id}:analyze?api-version={CU_API_VERSION}"
    )
    headers = {
        "Ocp-Apim-Subscription-Key": api_key,
        "Content-Type": content_type,
    }

    print(f"\n[CU] POST  {url}")
    resp = requests.post(url, headers=headers, data=file_bytes, timeout=90)

    if resp.status_code == 200:
        return resp.json()

    if resp.status_code == 202:
        op_url = resp.headers.get("operation-location") or resp.headers.get("Operation-Location")
        if not op_url:
            print("[CU] No se recibió operation-location en la respuesta 202.")
            sys.exit(1)
        return _poll_operation(op_url, api_key, label="CU", timeout_s=180)

    _abort("CU", resp)


# ─────────────────────────────────────────────────────────────────────────────
#  Document Intelligence  —  Clasificación  (DI-Classify)
# ─────────────────────────────────────────────────────────────────────────────

def probe_di_classify(endpoint: str, api_key: str, classifier_id: str,
                      file_bytes: bytes) -> dict:
    endpoint = endpoint.rstrip("/")
    url = (
        f"{endpoint}/documentintelligence/documentClassifiers/"
        f"{classifier_id}:analyze"
        f"?_overload=classifyDocument&api-version={DI_API_VERSION}"
    )
    headers = {
        "Ocp-Apim-Subscription-Key": api_key,
        "Content-Type": "application/json",
    }
    body = json.dumps({"base64Source": base64.b64encode(file_bytes).decode()})

    print(f"\n[DI-Classify] POST  {url}")
    resp = requests.post(url, headers=headers, data=body, timeout=60)

    if resp.status_code in (200, 201):
        return resp.json()

    if resp.status_code == 202:
        op_url = resp.headers.get("operation-location") or resp.headers.get("Operation-Location")
        if not op_url:
            print("[DI-Classify] No se recibió operation-location en la respuesta 202.")
            sys.exit(1)
        return _poll_operation(op_url, api_key, label="DI-Classify", timeout_s=120)

    _abort("DI-Classify", resp)


# ─────────────────────────────────────────────────────────────────────────────
#  Document Intelligence  —  Layout Markdown  (DI-Layout)
#  Opcional; usa documentModels/{modelId}:analyze con features=queryFields,
#  outputContentFormat=markdown.
# ─────────────────────────────────────────────────────────────────────────────

def probe_di_layout(endpoint: str, api_key: str, model_id: str,
                    file_bytes: bytes, api_version: str = DI_API_VERSION) -> dict:
    endpoint = endpoint.rstrip("/")
    url = (
        f"{endpoint}/documentintelligence/documentModels/"
        f"{model_id}:analyze"
        f"?api-version={api_version}&outputContentFormat=markdown"
    )
    headers = {
        "Ocp-Apim-Subscription-Key": api_key,
        "Content-Type": "application/json",
    }
    body = json.dumps({"base64Source": base64.b64encode(file_bytes).decode()})

    print(f"\n[DI-Layout] POST  {url}")
    resp = requests.post(url, headers=headers, data=body, timeout=90)

    if resp.status_code in (200, 201):
        return resp.json()

    if resp.status_code == 202:
        op_url = resp.headers.get("operation-location") or resp.headers.get("Operation-Location")
        if not op_url:
            print("[DI-Layout] No se recibió operation-location en la respuesta 202.")
            sys.exit(1)
        return _poll_operation(op_url, api_key, label="DI-Layout", timeout_s=180)

    _abort("DI-Layout", resp)


# ─────────────────────────────────────────────────────────────────────────────
#  Main
# ─────────────────────────────────────────────────────────────────────────────

def main() -> None:
    ap = argparse.ArgumentParser(
        description="Sonda de usage / pages / tokens en respuestas de Azure CU y DI.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    ap.add_argument("--file", required=True, help="PDF o imagen de entrada")
    ap.add_argument("--config", help="JSON config file with endpoints/keys (overridden by CLI args)")

    # CU
    ap.add_argument("--cu-endpoint", help="Endpoint CU  (o env CU_ENDPOINT)")
    ap.add_argument("--cu-key",      help="API key CU   (o env CU_KEY)")
    ap.add_argument("--cu-analyzer", default="CU_NS_1.4_2", help="Analyzer ID [CU_NS_1.4_2]")

    # DI Classify
    ap.add_argument("--di-endpoint",   help="Endpoint DI  (o env DI_ENDPOINT)")
    ap.add_argument("--di-key",        help="API key DI   (o env DI_KEY)")
    ap.add_argument("--di-classifier", help="Classifier ID  (DI clasificación)")

    # DI Layout (opcional)
    ap.add_argument("--di-layout-model",   help="Model ID para DI layout markdown (opcional)")
    ap.add_argument("--di-api-version",    default=DI_API_VERSION, help=f"API version DI [{DI_API_VERSION}]")

    args = ap.parse_args()

    # Validar fichero de entrada
    if not os.path.isfile(args.file):
        print(f"ERROR: Fichero no encontrado: {args.file}")
        sys.exit(1)

    with open(args.file, "rb") as fh:
        file_bytes = fh.read()

    ct = content_type_for(args.file)
    out_dir = os.path.dirname(os.path.abspath(args.file))
    # Cargar configuración desde JSON (si existe). Precedencia: CLI > config file > env vars
    config: dict = {}
    config_path = args.config or None
    if not config_path:
        candidates = [
            os.path.join(out_dir, "probe_usage_tokens.json"),
            os.path.join(os.getcwd(), "probe_usage_tokens.json"),
            (os.path.join(os.path.dirname(__file__), "probe_usage_tokens.json") if "__file__" in globals() else None),
            os.path.expanduser("~/.probe_usage_tokens.json"),
        ]
        for c in candidates:
            if c and os.path.isfile(c):
                config_path = c
                break

    if config_path and os.path.isfile(config_path):
        try:
            with open(config_path, "r", encoding="utf-8") as fh:
                loaded = json.load(fh)
                if isinstance(loaded, dict):
                    config = loaded
                    print(f"  [config] Cargada configuración desde {config_path}")
                else:
                    print(f"  [config] Ignorando JSON no-objeto en {config_path}")
        except Exception as exc:
            print(f"  [config] Error leyendo {config_path}: {exc}")

    ran_any = False

    # ─ CU ──────────────────────────────────────────────────────────────
    cu_endpoint = args.cu_endpoint or config.get("cu_endpoint") or os.environ.get("CU_ENDPOINT", "")
    cu_key      = args.cu_key      or config.get("cu_key") or os.environ.get("CU_KEY", "")
    cu_analyzer = args.cu_analyzer or config.get("cu_analyzer") or "CU_NS_1.4_2"

    if cu_endpoint and cu_key:
        result = probe_cu(cu_endpoint, cu_key, cu_analyzer, file_bytes, ct)
        out = os.path.join(out_dir, "cu_probe_response.json")
        save_json(result, out, "Content Understanding  —  respuesta completa")
        summarize_interesting("Content Understanding", result)
        ran_any = True
    elif cu_endpoint or cu_key:
        print("WARN: --cu-endpoint y --cu-key deben especificarse juntos. Saltando CU.")

    # ─ DI Classify ──────────────────────────────────────────────────────
    di_endpoint = args.di_endpoint or config.get("di_endpoint") or os.environ.get("DI_ENDPOINT", "")
    di_key      = args.di_key      or config.get("di_key") or os.environ.get("DI_KEY", "")
    di_classifier = args.di_classifier or config.get("di_classifier")
    di_layout_model = args.di_layout_model or config.get("di_layout_model")
    di_api_version = args.di_api_version or config.get("di_api_version") or DI_API_VERSION

    if di_endpoint and di_key and di_classifier:
        result = probe_di_classify(di_endpoint, di_key, di_classifier, file_bytes)
        out = os.path.join(out_dir, "di_probe_response.json")
        save_json(result, out, "Document Intelligence Classify  —  respuesta completa")
        summarize_interesting("Document Intelligence Classify", result)
        ran_any = True
    elif di_endpoint or di_key or di_classifier:
        print("WARN: --di-endpoint, --di-key y --di-classifier deben especificarse juntos. Saltando DI Classify.")

    # ─ DI Layout (opcional) ─────────────────────────────────────────────
    if di_endpoint and di_key and di_layout_model:
        result = probe_di_layout(di_endpoint, di_key, di_layout_model,
                                 file_bytes, api_version=di_api_version)
        out = os.path.join(out_dir, "di_layout_probe_response.json")
        save_json(result, out, "Document Intelligence Layout Markdown  —  respuesta completa")
        summarize_interesting("Document Intelligence Layout Markdown", result)
        ran_any = True

    if not ran_any:
        print("\nNo se ha ejecutado ningún servicio.")
        ap.print_help()
        sys.exit(1)

    print("\n[Done]  Revisa los ficheros .json en el directorio del documento.")


if __name__ == "__main__":
    main()
