import sys
import pathlib
import pytest
import pandas as pd

# Ensure module path so tests can import ActivoEnrichment when running from repo root
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from ActivoEnrichment import enriquecer, IntegracionRequest, DatosExtraidos, _activos_cache


def make_dummy_df():
    df = pd.DataFrame([
        {"ID_ACTIVO_SAREB": "123", "SERVICER": "SERV1", "TIPOLOGIA": "TIP1", "ID_REF_CATAST": "REF-A"},
        {"ID_ACTIVO_SAREB": "456", "SERVICER": "SERV2", "TIPOLOGIA": "TIP2", "ID_REF_CATAST": "REF-B"},
    ])
    return df


def test_enriquecer_preserves_incoming_idActivo(monkeypatch):
    # Inject dummy activos dataframe into module cache
    df = make_dummy_df()
    monkeypatch.setattr("ActivoEnrichment._activos_cache", df, raising=False)

    req = IntegracionRequest(
        tipologia="nota",
        documentoId=None,
        idActivo="EXISTING-1",
        datosExtraidos=DatosExtraidos(ReferenciaCatastral="REF-A"),
        metadata={}
    )

    result = enriquecer(req)
    assert result.get("idActivo") == "EXISTING-1"


def test_enriquecer_returns_found_idActivo_when_not_in_request(monkeypatch):
    df = make_dummy_df()
    monkeypatch.setattr("ActivoEnrichment._activos_cache", df, raising=False)

    req = IntegracionRequest(
        tipologia="nota",
        documentoId=None,
        idActivo=None,
        datosExtraidos=DatosExtraidos(ReferenciaCatastral="REF-B"),
        metadata={}
    )

    result = enriquecer(req)
    assert result.get("idActivo") == "456"
