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
"""
import re
import sys

ROW_RE = re.compile(r"^\s{4}\((.*)\)[,]?\s*$")
MERGE_RE = re.compile(r"^MERGE INTO \[dbo\]\.\[(\w+)\] AS tgt")
COLS_RE = re.compile(r"^\)\s+AS src \((.*)\)\s*$")


def tokenize(line):
    """Divide una fila '(v1, v2, N'texto', ...)' en valores; respeta '' dentro de cadenas."""
    vals, i, n = [], 0, len(line)
    while i < n:
        while i < n and line[i] in " \t":
            i += 1
        if i >= n:
            break
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
        else:
            j = i
            while j < n and line[j] != ",":
                j += 1
            tok = line[i:j].strip()
            vals.append(None if tok.upper() == "NULL" else tok)
            i = j
        while i < n and line[i] in " \t":
            i += 1
        if i < n and line[i] == ",":
            i += 1
    return vals


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
        entry = tables.setdefault(t, {"rows": [], "cols": [c.strip().strip("[]") for c in cols.split(",")]})
        entry["rows"].extend(tokenize(r) for r in split_rows(block))
    return tables


def short(v, n=70):
    if v is None:
        return "NULL"
    s = str(v).replace("\n", "\\n")
    return s if len(s) <= n else s[:n] + "…(" + str(len(s)) + " chars)"


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    a_path, b_path = sys.argv[1], sys.argv[2]
    only = sys.argv[3:]
    A, B = parse(a_path), parse(b_path)
    la, lb = a_path.split("/")[-1].split("\\")[-1], b_path.split("/")[-1].split("\\")[-1]
    # Argumento "Tabla=Col" compara por esa columna (clave natural) en vez de por PK.
    keys = {a.split("=")[0]: (a.split("=")[1] if "=" in a else None) for a in only}
    only = list(keys)
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


if __name__ == "__main__":
    main()
