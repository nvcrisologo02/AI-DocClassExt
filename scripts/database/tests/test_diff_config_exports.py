"""Pruebas del modo --emit-inserts de diff-config-exports.py (AB#101075).

Uso: python -m unittest discover -s scripts/database/tests -p "test_*.py"
"""
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), "..", "diff-config-exports.py")
_spec = importlib.util.spec_from_file_location("diff_config_exports", SCRIPT)
dce = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dce)

KEYS = ["ModeloConfigs=Key", "PromptTemplates=PromptKey+Version", "Tipologias=Codigo",
        "CatalogoTdn1=Codigo", "CatalogoTdn2=Codigo", "PluginTipologiaConfigs=TipologiaCodigo"]


def export(blocks):
    """Construye un export con el formato de replicate-config-data.ps1."""
    out = []
    for table, cols, rows in blocks:
        out.append(f"MERGE INTO [dbo].[{table}] AS tgt")
        out.append("USING (VALUES")
        out.append(",\r\n".join("    (" + r + ")" for r in rows))
        out.append(") AS src (" + ", ".join(f"[{c}]" for c in cols) + ")")
        out.append("ON tgt.[Id] = src.[Id]")
        out.append("WHEN NOT MATCHED BY TARGET THEN INSERT (...) VALUES (...);")
        out.append("")
    return "\n".join(out)


TDN1 = ["Id", "Codigo", "Nombre", "Descripcion", "TDN2_Prompt"]
TDN2 = ["Id", "Codigo", "Nombre", "Descripcion", "CodigoTdn1", "Tdn1Id"]
TIPO = ["Id", "Codigo", "Nombre", "Version", "Activa", "ConfiguracionJson", "FechaCreacion",
        "FechaActualizacion", "CreadoPor", "Estado", "PublicadaEn", "PublicadaPor", "VersionPublicada"]
MODELO = ["Id", "Tipo", "Key", "Provider", "Activo", "ConfiguracionJson", "FechaCreacion",
          "FechaActualizacion", "CreadoPor"]


def write(tmp, name, text):
    path = os.path.join(tmp, name)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return path


class EmitInsertsTests(unittest.TestCase):
    def build(self, destino, origen):
        with tempfile.TemporaryDirectory() as tmp:
            a = dce.parse(write(tmp, "destino.sql", export(destino)))
            b = dce.parse(write(tmp, "origen.sql", export(origen)))
        return dce.build_inserts(a, b, dce.parse_keys(KEYS))

    def test_sin_filas_nuevas_no_genera_inserts(self):
        bloque = [("CatalogoTdn1", TDN1, ["(1, N'LIPR', N'Licencias', NULL, NULL)"])]
        res = self.build(bloque, bloque)
        self.assertEqual(res.inserted, [])
        self.assertNotIn("INSERT INTO", res.sql)

    def test_alta_sin_id_e_idempotente_por_clave_natural(self):
        destino = [("Tipologias", TIPO, ["(7, N'otra', N'Otra', 1, 1, N'{}', '2026-01-01T00:00:00.0000000', NULL, N'x', N'Publicada', NULL, NULL, 1)"])]
        origen = [("Tipologias", TIPO, [
            "(7, N'otra', N'Otra', 1, 1, N'{}', '2026-01-01T00:00:00.0000000', NULL, N'x', N'Publicada', NULL, NULL, 1)",
            "(900, N'lipr.13', N'Licencia de l''obra', 1, 1, N'{\"a\":1}', '2026-10-21T10:00:00.0000000', NULL, N'seed', N'Publicada', NULL, NULL, 1)"])]
        res = self.build(destino, origen)
        self.assertEqual(res.inserted, [("Tipologias", "lipr.13")])
        self.assertIn("INSERT INTO [dbo].[Tipologias] ([Codigo], [Nombre], [Version]", res.sql)
        self.assertNotIn("900", res.sql)
        self.assertIn("N'Licencia de l''obra'", res.sql)
        self.assertIn("WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Tipologias] WHERE [Codigo] = N'lipr.13')", res.sql)
        self.assertNotIn("UPDATE", res.sql.upper().replace("UPDATEDAT", ""))
        self.assertNotIn("DELETE", res.sql.upper())

    def test_filas_distintas_no_se_tocan(self):
        destino = [("CatalogoTdn1", TDN1, ["(1, N'LIPR', N'Viejo', NULL, NULL)"])]
        origen = [("CatalogoTdn1", TDN1, ["(5, N'LIPR', N'Nuevo', NULL, NULL)"])]
        res = self.build(destino, origen)
        self.assertEqual(res.inserted, [])

    def test_tdn1id_se_resuelve_por_codigo_en_destino(self):
        destino = [("CatalogoTdn1", TDN1, ["(3, N'LIPR', N'Licencias', NULL, NULL)"]), ("CatalogoTdn2", TDN2, [])]
        origen = [("CatalogoTdn1", TDN1, ["(40, N'LIPR', N'Licencias', NULL, NULL)"]),
                  ("CatalogoTdn2", TDN2, ["(1200, N'LIPR-13', N'Obra: licencia segunda ocupación', N'd', N'LIPR', 40)"])]
        res = self.build(destino, origen)
        self.assertEqual(res.inserted, [("CatalogoTdn2", "LIPR-13")])
        self.assertIn("(SELECT [Id] FROM [dbo].[CatalogoTdn1] WHERE [Codigo] = N'LIPR')", res.sql)
        self.assertNotIn(", 40", res.sql)

    def test_orden_de_tablas_respeta_dependencias(self):
        origen = [("Tipologias", TIPO, ["(1, N't', N'T', 1, 1, N'{}', '2026-01-01T00:00:00.0000000', NULL, NULL, N'Publicada', NULL, NULL, 1)"]),
                  ("CatalogoTdn2", TDN2, ["(2, N'NEW-01', N'n', NULL, N'NEW', 9)"]),
                  ("CatalogoTdn1", TDN1, ["(9, N'NEW', N'n', NULL, NULL)"])]
        res = self.build([], origen)
        self.assertEqual([t for t, _ in res.inserted], ["CatalogoTdn1", "CatalogoTdn2", "Tipologias"])

    def test_modeloconfigs_recoge_alias(self):
        js = '{"Deployment":"gpt","resourceAlias":"openai_primary","Extra":{"ResourceAlias":"di"},"AuthMode":"ApiKey","ApiKey":""}'
        origen = [("ModeloConfigs", MODELO, [f"(5, 1, N'gpt5-mini', N'AzureOpenAI', 1, N'{js}', '2026-01-01T00:00:00.0000000', NULL, NULL)"])]
        res = self.build([], origen)
        self.assertEqual(res.inserted, [("ModeloConfigs", "gpt5-mini")])
        self.assertEqual(res.aliases, ["di", "openai_primary"])

    def test_modeloconfigs_con_endpoint_o_apikey_se_rechaza(self):
        for js in ('{"Endpoint":"https://x.openai.azure.com"}', '{"a":{"apiKey":"secreto"}}'):
            origen = [("ModeloConfigs", MODELO, [f"(5, 1, N'm', N'AzureOpenAI', 1, N'{js}', '2026-01-01T00:00:00.0000000', NULL, NULL)"])]
            with self.assertRaises(ValueError) as ctx:
                self.build([], origen)
            self.assertIn("m", str(ctx.exception))

    def test_modeloconfigs_con_json_invalido_se_rechaza(self):
        origen = [("ModeloConfigs", MODELO, ["(5, 1, N'm', N'AzureOpenAI', 1, N'{no json', '2026-01-01T00:00:00.0000000', NULL, NULL)"])]
        with self.assertRaises(ValueError):
            self.build([], origen)

    def test_sql_va_en_una_transaccion(self):
        origen = [("CatalogoTdn1", TDN1, ["(9, N'NEW', N'n', NULL, NULL)"])]
        res = self.build([], origen)
        self.assertTrue(res.sql.lstrip().startswith("SET XACT_ABORT ON;"))
        self.assertIn("BEGIN TRANSACTION;", res.sql)
        self.assertTrue(res.sql.rstrip().endswith("COMMIT TRANSACTION;"))


class CliTests(unittest.TestCase):
    def test_cli_escribe_sql_y_alias(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = write(tmp, "destino.sql", export([("CatalogoTdn1", TDN1, ["(1, N'A', N'a', NULL, NULL)"])]))
            js = '{"ResourceAlias":"cu_primary"}'
            b = write(tmp, "origen.sql", export([
                ("ModeloConfigs", MODELO, [f"(5, 1, N'cu', N'CU', 1, N'{js}', '2026-01-01T00:00:00.0000000', NULL, NULL)"]),
                ("CatalogoTdn1", TDN1, ["(1, N'A', N'a', NULL, NULL)", "(2, N'B', N'b', NULL, NULL)"])]))
            out_sql = os.path.join(tmp, "altas.sql")
            out_alias = os.path.join(tmp, "alias.txt")
            r = subprocess.run([sys.executable, SCRIPT, a, b, *KEYS, "--emit-inserts", out_sql, "--aliases-out", out_alias],
                               capture_output=True, text=True, encoding="utf-8")
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("solo en B 1", r.stdout)
            with open(out_sql, encoding="utf-8") as f:
                sql = f.read()
            self.assertIn("INSERT INTO [dbo].[ModeloConfigs]", sql)
            self.assertIn("N'B'", sql)
            with open(out_alias, encoding="utf-8") as f:
                self.assertEqual(f.read().split(), ["cu_primary"])
            self.assertIn("altas: 2", r.stdout)

    def test_cli_falla_con_endpoint_en_modeloconfigs(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = write(tmp, "destino.sql", "")
            js = '{"Endpoint":"https://x"}'
            b = write(tmp, "origen.sql", export([("ModeloConfigs", MODELO, [f"(5, 1, N'm', N'X', 1, N'{js}', '2026-01-01T00:00:00.0000000', NULL, NULL)"])]))
            out_sql = os.path.join(tmp, "altas.sql")
            r = subprocess.run([sys.executable, SCRIPT, a, b, *KEYS, "--emit-inserts", out_sql],
                               capture_output=True, text=True, encoding="utf-8")
            self.assertNotEqual(r.returncode, 0)
            self.assertFalse(os.path.exists(out_sql))


if __name__ == "__main__":
    unittest.main()
