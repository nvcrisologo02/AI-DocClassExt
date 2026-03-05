from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Dict, Any, Optional
import pandas as pd
from pathlib import Path

# Ruta y hoja del Excel con los activos
EXCEL_PATH = Path(__file__).resolve().parent / "Activos.xlsx"
SHEET_INDEX = 0

app = FastAPI(title="Plugin Enriquecimiento Ref Catastral")


class DatosExtraidos(BaseModel):
    # Campo que nos interesa para buscar en el Excel
    ReferenciaCatastral: Optional[str] = None

    class Config:
        # Permitimos campos adicionales sin validacion estricta
        extra = "allow"


class IntegracionRequest(BaseModel):
    # Este modelo replica la estructura que envia IntegrarActivity a traves del RestPlugin
    tipologia: str
    documentoId: Optional[str] = None 
    # IdActivo puede venir en la petición (viene ahora en el payload desde IntegrarActivity)
    idActivo: Optional[str] = None
    datosExtraidos: DatosExtraidos
    metadata: Dict[str, Any]


# Cache simple del Excel en memoria para no leerlo en cada llamada
_activos_cache: Optional[pd.DataFrame] = None


def cargar_activos() -> pd.DataFrame:
    """
    Carga el Excel de activos.
    Se asume una unica pestaña con las columnas:
    ID_ACTIVO_SAREB, SERVICER, TIPOLOGIA, ID_REF_CATAST
    """
    df = pd.read_excel(EXCEL_PATH, sheet_name=SHEET_INDEX, dtype=str)
    df.columns = [c.strip() for c in df.columns]
    return df


def obtener_activos() -> pd.DataFrame:
    """
    Devuelve el DataFrame cacheado; si aun no se ha cargado,
    lee el Excel y lo guarda en memoria.
    """
    global _activos_cache
    if _activos_cache is None:
        _activos_cache = cargar_activos()
    return _activos_cache


@app.post("/enriquecer")
def enriquecer(request: IntegracionRequest) -> Dict[str, Any]:
    ref = (request.datosExtraidos.ReferenciaCatastral or "").strip()
    print(f"DEBUG: Buscando ref='{ref}'")

    if not ref:
        print("DEBUG: Sin ReferenciaCatastral → vacio")
        return {}

    df = obtener_activos()
    print(f"DEBUG: Columnas: {list(df.columns)}")
    
    if "ID_REF_CATAST" not in df.columns:
        print("DEBUG: Columna ID_REF_CATAST NO encontrada")
        raise HTTPException(status_code=500, detail="Columna ID_REF_CATAST faltante")

    # **FIX: Normalizar y buscar case insensitive**
    df['ID_REF_CATAST_CLEAN'] = df["ID_REF_CATAST"].astype(str).str.strip()
    ref_clean = ref.upper()
    filas = df[df['ID_REF_CATAST_CLEAN'] == ref_clean]
    
    print(f"DEBUG: Filas encontradas: {len(filas)}")
    print(f"DEBUG: Primeras 5 refs limpias: {df['ID_REF_CATAST_CLEAN'].head().tolist()}")

    if filas.empty:
        print(f"DEBUG: NO match para '{ref}'")
        return {}

    # **FIX: Verificar que filas no este vacio antes de iloc**
    fila = filas.iloc[0]
    print(f"DEBUG: Fila encontrada: {fila.to_dict()}")

    try:
        id_activo = int(str(fila["ID_ACTIVO_SAREB"]).strip())
    except:
        id_activo = 0

    resultado = {
        "id_activo_sareb": id_activo,
        "servicer": str(fila["SERVICER"]).strip(),
        "tipologia_activo": str(fila["TIPOLOGIA"]).strip(),
        "id_ref_catast": str(fila["ID_REF_CATAST"]).strip(),
    }
    # Propagar idActivo para compatibilidad con pipeline (clave esperada: "idActivo")
    # Si la petición ya traía un idActivo, preservarlo; sino usar el id_activo_sareb hallado
    if request.idActivo:
        resultado["idActivo"] = request.idActivo
    else:
        if id_activo and id_activo > 0:
            resultado["idActivo"] = str(id_activo)
    
    print(f"DEBUG: RESULTADO: {resultado}")
    return resultado


@app.post("/")
@app.post("/api/process")
@app.post("")
def enriquecer_raiz(request: IntegracionRequest) -> Dict[str, Any]:
    """
    Captura las llamadas que llegan a la raiz o api/process
    (lo que usa RestPlugin por defecto).
    """
    print(f"DEBUG RAIZ: Payload: {request.json()}")
    return enriquecer(request)


@app.get("/health")
def health():
    """Health check para RestPlugin."""
    return {"status": "OK", "service": "ActivoEnrichment"}

# Para desarrollo local:
# uvicorn ActivoEnrichment:app --reload --port 8080
