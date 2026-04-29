from pathlib import Path

from docx import Document
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer


def parse_markdown_lines(md_path: Path):
    lines = md_path.read_text(encoding="utf-8").splitlines()
    parsed = []
    for raw in lines:
        line = raw.rstrip()
        if not line:
            parsed.append(("blank", ""))
            continue
        if line.startswith("### "):
            parsed.append(("h3", line[4:].strip()))
        elif line.startswith("## "):
            parsed.append(("h2", line[3:].strip()))
        elif line.startswith("# "):
            parsed.append(("h1", line[2:].strip()))
        elif line.startswith("- "):
            parsed.append(("bullet", line[2:].strip()))
        elif line[:3].isdigit() and line[1:3] == ". ":
            parsed.append(("number", line.strip()))
        else:
            parsed.append(("p", line))
    return parsed


def md_to_docx(md_path: Path, docx_path: Path):
    doc = Document()
    for kind, text in parse_markdown_lines(md_path):
        if kind == "blank":
            doc.add_paragraph("")
        elif kind == "h1":
            doc.add_heading(text, level=1)
        elif kind == "h2":
            doc.add_heading(text, level=2)
        elif kind == "h3":
            doc.add_heading(text, level=3)
        elif kind == "bullet":
            doc.add_paragraph(text, style="List Bullet")
        elif kind == "number":
            doc.add_paragraph(text, style="List Number")
        else:
            doc.add_paragraph(text)
    doc.save(docx_path)


def md_to_pdf(md_path: Path, pdf_path: Path):
    styles = getSampleStyleSheet()
    h1 = ParagraphStyle(
        "H1",
        parent=styles["Heading1"],
        fontSize=16,
        leading=19,
        spaceAfter=10,
    )
    h2 = ParagraphStyle(
        "H2",
        parent=styles["Heading2"],
        fontSize=13,
        leading=16,
        spaceAfter=8,
    )
    h3 = ParagraphStyle(
        "H3",
        parent=styles["Heading3"],
        fontSize=11,
        leading=14,
        spaceAfter=6,
    )
    body = ParagraphStyle(
        "Body",
        parent=styles["BodyText"],
        fontSize=10,
        leading=13,
        spaceAfter=5,
    )
    bullet = ParagraphStyle(
        "Bullet",
        parent=body,
        leftIndent=12,
    )

    doc = SimpleDocTemplate(
        str(pdf_path),
        pagesize=A4,
        leftMargin=2 * cm,
        rightMargin=2 * cm,
        topMargin=2 * cm,
        bottomMargin=2 * cm,
    )

    story = []
    for kind, text in parse_markdown_lines(md_path):
        safe_text = (
            text.replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;")
        )
        if kind == "blank":
            story.append(Spacer(1, 6))
        elif kind == "h1":
            story.append(Paragraph(safe_text, h1))
        elif kind == "h2":
            story.append(Paragraph(safe_text, h2))
        elif kind == "h3":
            story.append(Paragraph(safe_text, h3))
        elif kind == "bullet":
            story.append(Paragraph("• " + safe_text, bullet))
        elif kind == "number":
            story.append(Paragraph(safe_text, body))
        else:
            story.append(Paragraph(safe_text, body))

    doc.build(story)


def main():
    base = Path(__file__).resolve().parent

    docs = [
        (
            base / "Documento_Riesgos_IA_Completo.md",
            base / "Documento_Riesgos_IA_Completo.docx",
            base / "Documento_Riesgos_IA_Completo.pdf",
        ),
        (
            base / "Documento_Riesgos_IA_Resumen.md",
            base / "Documento_Riesgos_IA_Resumen.docx",
            base / "Documento_Riesgos_IA_Resumen.pdf",
        ),
    ]

    for md_path, docx_path, pdf_path in docs:
        md_to_docx(md_path, docx_path)
        md_to_pdf(md_path, pdf_path)

    print("Generados:")
    for _, docx_path, pdf_path in docs:
        print(docx_path)
        print(pdf_path)


if __name__ == "__main__":
    main()
