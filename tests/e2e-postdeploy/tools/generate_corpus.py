"""Genera el corpus sintetico del E2E post-despliegue.

Uso (desde la raiz del repo, con .venv activado):
    pip install reportlab python-docx openpyxl
    python tests/e2e-postdeploy/tools/generate_corpus.py

Los documentos contienen exclusivamente datos ficticios.
"""
from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm
from reportlab.pdfgen import canvas
from docx import Document
from openpyxl import Workbook

ROOT = Path(__file__).resolve().parents[1] / "corpus"

NOTA_SIMPLE = [
    "REGISTRO DE LA PROPIEDAD N. 99 DE VILLAFICTICIA",
    "NOTA SIMPLE INFORMATIVA",
    "",
    "FINCA DE VILLAFICTICIA N: 12345  IDUFIR: 99999999999999",
    "DESCRIPCION: URBANA. Vivienda en planta segunda, puerta B, del edificio",
    "sito en Calle Imaginaria numero 8, de Villaficticia. Superficie construida:",
    "ochenta y cinco metros cuadrados. Cuota de participacion: 4,25 por ciento.",
    "",
    "TITULARIDADES: Don Fulano Ejemplar Perez, con DNI 00000000-X, pleno dominio",
    "de la totalidad de la finca, por titulo de compraventa formalizado en",
    "escritura publica el 15 de marzo de dos mil veinte.",
    "",
    "CARGAS: Hipoteca a favor de Banco Ficticio S.A. por importe de principal de",
    "ciento veinte mil euros, constituida en escritura de fecha 15 de marzo de",
    "dos mil veinte. Libre de otras cargas y gravamenes.",
    "",
    "Esta nota simple tiene valor puramente informativo, conforme al articulo",
    "332 del Reglamento Hipotecario.",
]

TASACION = [
    "INFORME DE TASACION",
    "SOCIEDAD DE TASACIONES FICTICIA S.A. - Numero de expediente: TAS-2026-0001",
    "",
    "SOLICITANTE: Banco Ficticio S.A.",
    "FINALIDAD: Garantia hipotecaria de prestamo (Orden ECO/805/2003).",
    "",
    "IDENTIFICACION DEL INMUEBLE: Vivienda sita en Calle Imaginaria 8, 2B,",
    "Villaficticia. Referencia catastral 0000000XX0000X0000XX. Finca registral",
    "12345 del Registro de la Propiedad N. 99 de Villaficticia.",
    "",
    "SUPERFICIES: Construida 85 m2. Util 72 m2.",
    "METODO DE VALORACION: Comparacion. Se han utilizado seis testigos de",
    "mercado en un radio de 500 metros.",
    "",
    "VALOR DE TASACION: CIENTO CINCUENTA MIL EUROS (150.000 EUR).",
    "Valor por metro cuadrado: 1.764,71 EUR/m2.",
    "FECHA DE EMISION: 1 de junio de 2026. Tasador: Arquitecto colegiado 0000.",
]

ESCRITURA = [
    "ESCRITURA DE COMPRAVENTA",
    "NUMERO MIL DOSCIENTOS TREINTA Y CUATRO.",
    "",
    "En Villaficticia, mi residencia, a diez de mayo de dos mil veintiseis,",
    "ante mi, NOTARIO FICTICIO DEL ILUSTRE COLEGIO NOTARIAL, comparecen:",
    "DE UNA PARTE, como vendedora, PROMOCIONES IMAGINARIAS S.L., CIF B-00000000.",
    "DE OTRA PARTE, como compradora, Dona Mengana Ejemplar Ruiz, DNI 11111111-Y.",
    "",
    "EXPONEN: Que la parte vendedora es duena de la siguiente FINCA: URBANA,",
    "vivienda en Calle Imaginaria 8, 2B, de Villaficticia, finca registral 12345.",
    "",
    "OTORGAN: PRIMERO. Promociones Imaginarias S.L. VENDE Y TRANSMITE a Dona",
    "Mengana Ejemplar Ruiz, que COMPRA Y ADQUIERE, la finca descrita, por precio",
    "de CIENTO CUARENTA MIL EUROS (140.000 EUR).",
    "SEGUNDO. La parte compradora manifiesta conocer el estado de cargas.",
]

RECIBO_IBI = [
    "AYUNTAMIENTO DE VILLAFICTICIA",
    "IMPUESTO SOBRE BIENES INMUEBLES (IBI) - RECIBO",
    "",
    "EJERCICIO: 2026",
    "REFERENCIA CATASTRAL: 0000000XX0000X0000XX",
    "",
    "TITULAR: Don Fulano Ejemplar Perez",
    "DOMICILIO TRIBUTARIO: Calle Imaginaria numero 8, Villaficticia.",
    "",
    "BASE LIQUIDABLE: 45.000,00 EUR",
    "TIPO DE GRAVAMEN: 0,6941 por ciento",
    "CUOTA A INGRESAR: 312,45 EUR",
    "",
    "Este recibo tiene caracter puramente informativo y contiene datos",
    "integramente ficticios, de uso exclusivo para pruebas del sistema de",
    "clasificacion documental.",
]

INFORME_ACTIVO = [
    "INFORME DE SITUACION DE ACTIVO INMOBILIARIO",
    "Referencia interna: ACT-354937 (dato ficticio de prueba)",
    "",
    "El presente informe recoge la situacion del activo a fecha de emision:",
    "ocupacion, estado de conservacion, situacion registral y comercializacion.",
    "El activo se encuentra libre de ocupantes, con conservacion normal y",
    "sin incidencias registrales conocidas. Se recomienda continuar el plan",
    "de comercializacion ordinario y revisar la valoracion en el proximo",
    "trimestre. Este documento es sintetico y se emplea unicamente para",
    "pruebas post-despliegue del sistema de clasificacion documental.",
]


def write_pdf(path: Path, lines: list[str], pages: int = 1) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(path), pagesize=A4)
    _, height = A4
    for page in range(pages):
        y = height - 2 * cm
        c.setFont("Helvetica", 11)
        if pages > 1:
            c.drawString(2 * cm, y, f"Pagina {page + 1} de {pages}")
            y -= 1 * cm
        for line in lines:
            c.drawString(2 * cm, y, line)
            y -= 0.6 * cm
        c.showPage()
    c.save()
    print(f"OK  {path}")


def write_docx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    doc = Document()
    doc.add_heading("Comunicacion interna sintetica", level=1)
    doc.add_paragraph(
        "Comunicacion de prueba para validar la ingesta de formato DOCX en el "
        "pipeline de clasificacion documental. Contenido integramente ficticio."
    )
    doc.add_paragraph("Referencia: COM-2026-0001. Emisor: Departamento Imaginario.")
    doc.save(str(path))
    print(f"OK  {path}")


def write_xlsx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    wb = Workbook()
    ws = wb.active
    ws.title = "Activos"
    ws.append(["Referencia", "Municipio", "Superficie m2", "Estado"])
    ws.append(["ACT-000001", "Villaficticia", 85, "Disponible"])
    ws.append(["ACT-000002", "Villaimaginaria", 120, "Comercializado"])
    wb.save(str(path))
    print(f"OK  {path}")


def write_corrupt_pdf(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    # Cabecera PDF valida seguida de bytes truncados: no es un PDF procesable.
    path.write_bytes(b"%PDF-1.7\n%\xe2\xe3\xcf\xd3\n1 0 obj\n<< /Type /Catalog")
    print(f"OK  {path}")


def write_control_marcado(path: Path, pages: int = 12) -> None:
    """Documento control para el juego de validacion de cobertura de markdown.

    Cada pagina lleva un marcador unico y buscable (MARCA-PAGINA-NN, dos
    digitos). Eso hace que la cobertura del markdown sea observable
    directamente en la respuesta de la orquestacion, sin descomprimir el
    blob ni comparar tamanos.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(path), pagesize=A4)
    _, height = A4
    for page in range(1, pages + 1):
        c.setFont("Helvetica-Bold", 24)
        c.drawString(2 * cm, height - 3 * cm, f"MARCA-PAGINA-{page:02d}")
        c.setFont("Helvetica", 11)
        c.drawString(2 * cm, height - 4 * cm, f"Pagina {page} de {pages} del documento control de validacion.")
        c.drawString(2 * cm, height - 4.6 * cm, "Contenido sintetico sin datos reales.")
        c.showPage()
    c.save()
    print(f"OK  {path}")


if __name__ == "__main__":
    write_pdf(ROOT / "nota-simple" / "nota-simple-sintetica.pdf", NOTA_SIMPLE, pages=2)
    write_pdf(ROOT / "tasacion" / "tasacion-sintetica.pdf", TASACION, pages=2)
    write_pdf(ROOT / "generico" / "escritura-compraventa-sintetica.pdf", ESCRITURA, pages=2)
    write_pdf(ROOT / "resumen" / "informe-activo-sintetico.pdf", INFORME_ACTIVO, pages=1)
    write_pdf(ROOT / "cera" / "recibo-ibi-sintetico.pdf", RECIBO_IBI, pages=1)
    write_pdf(ROOT / "multipagina" / "documento-12-paginas.pdf", NOTA_SIMPLE, pages=12)
    write_docx(ROOT / "formatos" / "comunicacion-sintetica.docx")
    write_xlsx(ROOT / "formatos" / "listado-activos-sintetico.xlsx")
    write_corrupt_pdf(ROOT / "invalidos" / "corrupto.pdf")
    write_control_marcado(ROOT / "control" / "documento-12-paginas-marcado.pdf")
    print("Corpus generado.")
