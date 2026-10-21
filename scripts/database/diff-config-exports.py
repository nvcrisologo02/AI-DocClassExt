"""Diff fila a fila entre dos exports de configuracion de DocumentIA.

Compara dos ficheros .sql generados por scripts/ai/export-config-release.ps1 (o por
scripts/database/replicate-config-data.ps1 -Mode Export). Solo lectura: parsea los
bloques MERGE INTO [dbo].[T] ... USING (VALUES ...) y no toca ninguna BD.

Uso:
  python scripts/database/diff-config-exports.py <a.sql> <b.sql> [Tabla[=Col1[+Col2...]] ...]

Sin argumentos de tabla compara todas las tablas por clave primaria (primer valor de
cada fila, normalmente Id). Con "Tabla=Columna" compara solo esas tablas y por esa
columna (clave natural), ignorando Id y las columnas de auditoria. Una clave compuesta
se escribe con "+" (PromptTemplates=PromptKey+Version, indice unico del modelo). Si una
columna de clave no existe en el export, o la clave no es unica en un fichero, el
script termina con error en vez de comparar mal. Ejemplo de diff por clave natural
entre el export de un entorno y el de DEV (indices unicos del modelo):

  python scripts/database/diff-config-exports.py config-<entorno>.sql config-vX.Y.Z.sql \
    ModeloConfigs=Key PromptTemplates=PromptKey+Version Tipologias=Codigo CatalogoTdn1=Codigo \
    CatalogoTdn2=Codigo PluginTipologiaConfigs=TipologiaCodigo

Salida por tabla: filas en A y en B, solo en A, solo en B y distintas, con las
columnas que cambian. Los ficheros se leen sin traducir saltos de linea, asi que una
diferencia de CRLF frente a LF aparece como distinta.

Altas (AB#101075): con "--emit-inserts <altas.sql>" escribe ademas el SQL que da de alta
en A (destino) las filas que existen solo en B (origen), por clave natural, para las
tablas con "Tabla=Columna". Es el unico modo que genera escrituras y solo las escribe en
el fichero: nunca actualiza ni borra, no copia Id, resuelve CatalogoTdn2.Tdn1Id por
CodigoTdn1 en el destino y cada INSERT lleva WHERE NOT EXISTS por la clave, todo en una
transaccion. Rechaza (error, sin escribir el fichero) un ModeloConfig con Endpoint o ApiKey
con valor; "--aliases-out <alias.txt>" lista los ResourceAlias de las altas de
ModeloConfigs para comprobar sus App Settings en el destino.
"""
import json
import re
import sys
from collections import namedtuple

ROW_RE = re.compile(r"^\s{4}\((.*)\)[,]?\s*$")
MERGE_RE = re.compile(r"^MERGE INTO \[dbo\]\.\[(\w+)\] AS tgt")
COLS_RE = re.compile(r"^\)\s+AS src \((.*)\)\s*$")


def tokenize(line, raw=False):
    """Divide una fila '(v1, v2, N'texto', ...)' en valores; respeta '' dentro de cadenas.
    Con raw=True devuelve ademas cada literal tal cual (N'...', numero o NULL) para
    poder reescribirlo en SQL."""
    vals, raws, i, n = [], [], 0, len(line)
    while i < n:
        while i < n and line[i] in " \t":
            i += 1
        if i >= n:
            break
        start = i
        if line[i] == "N" and i + 1 < n and line[i + 1] == "'":
            i += 1
        if line[i] == "'":
            i += 1
            buf = []
            while i < n:
                if line[i] == "'":
                    if i + 1 < n and line[i + 1] == "'":
                        buf.append("'")
                        i += 2
                        continue
                    i += 1
                    break
                buf.append(line[i])
                i += 1
            vals.append("".join(buf))
            raws.append(line[start:i])
        else:
            j = i
            while j < n and line[j] != ",":
                j += 1
            tok = line[i:j].strip()
            vals.append(None if tok.upper() == "NULL" else tok)
            raws.append(tok)
            i = j
        while i < n and line[i] in " \t":
            i += 1
        if i < n and line[i] == ",":
            i += 1
    return (vals, raws) if raw else vals


def split_rows(block):
    """Divide el texto entre 'USING (VALUES' y ') AS src' en filas '(...)' de primer nivel,
    respetando cadenas N'...' (con '' escapado y saltos de linea dentro)."""
    rows, i, n = [], 0, len(block)
    while i < n:
        if block[i] == "(":
            depth, j, in_str = 0, i, False
            while j < n:
                ch = block[j]
                if in_str:
                    if ch == "'":
                        if j + 1 < n and block[j + 1] == "'":
                            j += 2
                            continue
                        in_str = False
                elif ch == "'":
                    in_str = True
                elif ch == "(":
                    depth += 1
                elif ch == ")":
                    depth -= 1
                    if depth == 0:
                        break
                j += 1
            rows.append(block[i + 1:j])
            i = j + 1
        else:
            i += 1
    return rows


def parse(path):
    tables = {}
    # newline="" para no traducir CRLF a LF: si no, se ocultan diferencias reales.
    with open(path, encoding="utf-8-sig", newline="") as f:
        text = f.read()
    for m in re.finditer(r"MERGE INTO \[dbo\]\.\[(\w+)\] AS tgt\s*USING \(VALUES\s*(.*?)\n\)\s+AS src \((.*?)\)", text, re.S):
        t, block, cols = m.group(1), m.group(2), m.group(3)
        # Una tabla grande se exporta en varios MERGE: se acumulan.
        entry = tables.setdefault(t, {"rows": [], "raws": [], "cols": [c.strip().strip("[]") for c in cols.split(",")]})
        for r in split_rows(block):
            vals, raws = tokenize(r, raw=True)
            entry["rows"].append(vals)
            entry["raws"].append(raws)
    return tables


def parse_keys(args):
    """'Tabla=Col1+Col2' -> {Tabla: 'Col1+Col2'}; sin '=' la clave es la PK (None)."""
    return {a.split("=")[0]: (a.split("=")[1] if "=" in a else None) for a in args}


# Orden de las altas: cada tabla solo depende de las anteriores (CatalogoTdn2.Tdn1Id
# de CatalogoTdn1; Tipologias y PluginTipologiaConfigs referencian por codigo).
INSERT_ORDER = ["ModeloConfigs", "CatalogoTdn1", "CatalogoTdn2", "PromptTemplates", "Tipologias", "PluginTipologiaConfigs"]
# Claves ajenas por Id que se resuelven en el destino por codigo: columna -> (tabla, columna de codigo en la fila).
FOREIGN_BY_CODE = {("CatalogoTdn2", "Tdn1Id"): ("CatalogoTdn1", "CodigoTdn1")}

Altas = namedtuple("Altas", "sql inserted aliases")


def _check_modelo(key, raw_json):
    """Rechaza un ModeloConfig con Endpoint o ApiKey con valor (el resolver da prioridad
    al endpoint explicito sobre el alias) y devuelve los ResourceAlias que usa."""
    try:
        doc = json.loads(raw_json or "{}")
    except ValueError as e:
        raise ValueError(f"ModeloConfigs {key}: ConfiguracionJson no es JSON valido ({e})")
    aliases, bad = set(), []

    def walk(o):
        if isinstance(o, dict):
            for k, v in o.items():
                kl = k.lower()
                if kl in ("endpoint", "apikey") and isinstance(v, str) and v.strip():
                    bad.append(k)
                elif kl == "resourcealias" and isinstance(v, str) and v.strip():
                    aliases.add(v.strip())
                walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)

    walk(doc)
    if bad:
        raise ValueError(f"ModeloConfigs {key}: lleva {', '.join(sorted(set(bad)))} con valor; solo se dan de alta filas con ResourceAlias")
    return aliases


def build_inserts(A, B, keys):
    """Altas de las filas que existen solo en B (origen) frente a A (destino), por clave
    natural. Nunca actualiza ni borra y nunca copia Id; cada INSERT lleva WHERE NOT EXISTS
    por la clave, asi que reejecutar el script no cambia nada."""
    out = ["SET XACT_ABORT ON;", "BEGIN TRANSACTION;", ""]
    inserted, aliases = [], set()
    for t in INSERT_ORDER:
        if not keys.get(t) or t not in B:
            continue
        cols, kcols = B[t]["cols"], keys[t].split("+")
        missing = [c for c in kcols if c not in cols]
        if missing:
            raise ValueError(f"la tabla {t} no tiene la columna de clave {', '.join(missing)}")
        kis = [cols.index(c) for c in kcols]
        target = {tuple(r[i] for i in kis) for r in A.get(t, {}).get("rows", [])}
        seen = set()
        ins_cols = [c for c in cols if c != "Id"]
        for vals, raws in zip(B[t]["rows"], B[t]["raws"]):
            k = tuple(vals[i] for i in kis)
            if k in seen:
                raise ValueError(f"clave {keys[t]}={k} repetida en el origen, tabla {t}")
            seen.add(k)
            if k in target:
                continue
            label = k[0] if len(k) == 1 else "+".join(str(x) for x in k)
            if t == "ModeloConfigs":
                aliases |= _check_modelo(label, vals[cols.index("ConfiguracionJson")])
            exprs = []
            for c in ins_cols:
                fk = FOREIGN_BY_CODE.get((t, c))
                if fk:
                    exprs.append(f"(SELECT [Id] FROM [dbo].[{fk[0]}] WHERE [Codigo] = {raws[cols.index(fk[1])]})")
                else:
                    exprs.append(raws[cols.index(c)])
            where = " AND ".join(f"[{c}] IS NULL" if vals[i] is None else f"[{c}] = {raws[i]}" for c, i in zip(kcols, kis))
            out.append(f"INSERT INTO [dbo].[{t}] ({', '.join(f'[{c}]' for c in ins_cols)})")
            out.append(f"SELECT {', '.join(exprs)}")
            out.append(f"WHERE NOT EXISTS (SELECT 1 FROM [dbo].[{t}] WHERE {where});")
            inserted.append((t, label))
    out += ["", "COMMIT TRANSACTION;"]
    return Altas("\n".join(out) + "\n", inserted, sorted(aliases))


def short(v, n=70):
    if v is None:
        return "NULL"
    s = str(v).replace("\n", "\\n")
    return s if len(s) <= n else s[:n] + "…(" + str(len(s)) + " chars)"


def pop_option(args, name):
    if name not in args:
        return None
    i = args.index(name)
    if i + 1 >= len(args):
        sys.exit(f"ERROR: {name} necesita una ruta")
    value = args[i + 1]
    del args[i:i + 2]
    return value


def main():
    args = sys.argv[1:]
    emit_path = pop_option(args, "--emit-inserts")
    aliases_path = pop_option(args, "--aliases-out")
    if len(args) < 2:
        print(__doc__)
        sys.exit(2)
    a_path, b_path = args[0], args[1]
    A, B = parse(a_path), parse(b_path)
    la, lb = a_path.split("/")[-1].split("\\")[-1], b_path.split("/")[-1].split("\\")[-1]
    # Argumento "Tabla=Col" compara por esa columna (clave natural) en vez de por PK.
    keys = parse_keys(args[2:])
    only = list(keys)
    altas = None
    if emit_path:
        # Las guardas se evaluan antes de imprimir el diff: si fallan no se escribe nada.
        try:
            altas = build_inserts(A, B, keys)
        except ValueError as e:
            sys.exit(f"ERROR: {e}")
    IGNORE = {"Id", "FechaCreacion", "FechaActualizacion", "FechaModificacion", "UpdatedAtUtc", "CreatedAtUtc", "CreadoPor", "PublicadaEn", "PublicadaPor"}
    for t in sorted(set(A) | set(B)):
        if only and t not in only:
            continue
        ra, rb = A.get(t, {}).get("rows", []), B.get(t, {}).get("rows", [])
        cols = A.get(t, {}).get("cols") or B.get(t, {}).get("cols") or []
        kcol = keys.get(t)
        kcols = kcol.split("+") if kcol else []
        missing = [c for c in kcols if c not in cols]
        if missing:
            sys.exit(f"ERROR: la tabla {t} no tiene la columna de clave {', '.join(missing)} (columnas: {', '.join(cols)})")
        kis = [cols.index(c) for c in kcols] if kcols else [0]
        if kcol:
            # La clave se lee antes de anular Id y auditoria (una clave compuesta puede incluir alguna).
            print(f"   (clave {kcol}; columnas: {', '.join(cols)}; se ignoran {', '.join(sorted(IGNORE & set(cols)))})")

        def keyed(rows, name):
            out = {}
            for r in rows:
                k = r[kis[0]] if len(kis) == 1 else tuple(r[i] for i in kis)
                if k in out:
                    sys.exit(f"ERROR: clave {kcol or 'PK'}={k} repetida en {name}, tabla {t}; use una clave unica (por ejemplo Col1+Col2)")
                out[k] = [None if (kcol and i < len(cols) and cols[i] in IGNORE) else v for i, v in enumerate(r)]
            return out

        ka = keyed(ra, a_path)
        kb = keyed(rb, b_path)
        order = lambda k: (len(str(k)), str(k))
        only_a = sorted(set(ka) - set(kb), key=order)
        only_b = sorted(set(kb) - set(ka), key=order)
        changed = []
        for k in ka:
            if k in kb and ka[k] != kb[k]:
                diffs = [(cols[i] if i < len(cols) else f"col{i}", ka[k][i], kb[k][i]) for i in range(max(len(ka[k]), len(kb[k]))) if (ka[k][i] if i < len(ka[k]) else None) != (kb[k][i] if i < len(kb[k]) else None)]
                changed.append((k, diffs))
        print(f"== {t}: {la} {len(ra)} filas | {lb} {len(rb)} filas | solo en A {len(only_a)} | solo en B {len(only_b)} | distintas {len(changed)}")
        label = lambda r: " | ".join(short(v, 40) for v in r[:4])
        for k in only_a:
            print(f"   solo en A  PK={k}: {label(ka[k])}")
        for k in only_b:
            print(f"   solo en B  PK={k}: {label(kb[k])}")
        for k, diffs in sorted(changed, key=lambda x: order(x[0])):
            cols_changed = ", ".join(d[0] for d in diffs)
            print(f"   distinta   PK={k} [{label(ka[k])}] -> {cols_changed}")
            for c, va, vb in diffs:
                if c in ("FechaActualizacion", "FechaModificacion", "UpdatedAt"):
                    continue
                print(f"        {c}: A={short(va)}")
                print(f"        {' ' * len(c)}  B={short(vb)}")

    if altas:
        with open(emit_path, "w", encoding="utf-8", newline="") as f:
            f.write(altas.sql)
        if aliases_path:
            with open(aliases_path, "w", encoding="utf-8", newline="") as f:
                f.write("".join(a + "\n" for a in altas.aliases))
        print(f"== altas: {len(altas.inserted)} (solo en B, por clave natural) -> {emit_path}")
        for t, k in altas.inserted:
            print(f"   alta {t} {k}")
        print(f"   alias de ModeloConfigs: {', '.join(altas.aliases) or '(ninguno)'}")


if __name__ == "__main__":
    main()
