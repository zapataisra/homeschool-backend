#!/usr/bin/env python3
"""PASO 2 DE 4: Extraccion masiva de los 30 libros CONALITEG (1.o a 6.o grado) a Markdown + Tablas.

Omite Programa_Sintetico_Fase_*.pdf (ya vectorizados en public.embeddings_sep) y archivos *_back.pdf.
Guarda en data/cache/{stem}.json el texto estructurado por pagina con tablas Markdown y metadatos.

Uso:
  python app/adapters/ingestion.py --all-books
  python app/adapters/ingestion.py --grade 4
"""

import argparse
import base64
import datetime
import hashlib
import json
import os
import re
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

import pymupdf as fitz

try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

REPO_ROOT = Path(__file__).resolve().parents[2]

BOOK_TYPE_MAP = {
    "NSA": "Nuestros Saberes",
    "SDA": "Nuestros Saberes",
    "PAA": "Proyectos de Aula",
    "PEA": "Proyectos Escolares",
    "PCA": "Proyectos Comunitarios",
    "MLA": "Multiples Lenguajes",
}

GRADE_TO_FASE = {
    1: "Fase 3",
    2: "Fase 3",
    3: "Fase 4",
    4: "Fase 4",
    5: "Fase 5",
    6: "Fase 5",
}

CONALITEG_FILENAME_RE = re.compile(r"^P([1-6])([A-Z]{3})_.*\.pdf$", re.IGNORECASE)


def sha256_file(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def infer_metadata(filename: str) -> dict | None:
    """Devuelve metadatos solo si es un libro CONALITEG de 1.o a 6.o grado (ignora Programa Sintetico y _back)."""
    if filename.lower().endswith("_back.pdf"):
        return None
    match_book = CONALITEG_FILENAME_RE.match(filename)
    if not match_book:
        return None

    grade = int(match_book.group(1))
    kind = match_book.group(2).upper()
    return {
        "book_code": f"P{grade}{kind}",
        "kind": kind,
        "grade": grade,
        "fase": GRADE_TO_FASE[grade],
        "book_type": BOOK_TYPE_MAP.get(kind, "Libro SEP"),
    }


def clean_cell(cell_val) -> str:
    if cell_val is None:
        return ""
    text = str(cell_val).replace("\n", " ").strip()
    return re.sub(r"\s+", " ", text)


def extract_tables_as_markdown(page: fitz.Page) -> list[str]:
    """Convierte tablas nativas del PDF a Markdown limpio."""
    md_tables = []
    try:
        finder = page.find_tables()
        for table in finder.tables:
            rows = table.extract()
            if not rows or len(rows) < 2:
                continue
            cleaned_rows = [[clean_cell(c) for c in row] for row in rows]
            cleaned_rows = [r for r in cleaned_rows if any(r)]
            if len(cleaned_rows) < 2:
                continue

            col_count = max(len(r) for r in cleaned_rows)
            if col_count < 2:
                continue

            norm_rows = [r + [""] * (col_count - len(r)) for r in cleaned_rows]
            header = "| " + " | ".join(norm_rows[0]) + " |"
            separator = "| " + " | ".join(["---"] * col_count) + " |"
            body = ["| " + " | ".join(r) for r in norm_rows[1:]]
            body = [row + " |" for row in body]
            md_tables.append("\n".join([header, separator, *body]))
    except Exception:
        pass
    return md_tables


def extract_structured_page_text(page: fitz.Page) -> str:
    """Extrae texto por bloques detectando encabezados por tamano de fuente."""
    blocks = page.get_text("dict", flags=fitz.TEXTFLAGS_TEXT).get("blocks", [])
    lines_out = []
    font_sizes = []

    for b in blocks:
        for line in b.get("lines", []):
            for span in line.get("spans", []):
                txt = span.get("text", "").strip()
                if txt:
                    font_sizes.append(span.get("size", 11.0))

    body_size = sorted(font_sizes)[len(font_sizes) // 2] if font_sizes else 11.0

    for b in blocks:
        block_lines = []
        max_size = 0.0
        for line in b.get("lines", []):
            spans = line.get("spans", [])
            line_text = "".join(s.get("text", "") for s in spans).strip()
            if not line_text:
                continue
            for s in spans:
                if s.get("size", 0.0) > max_size:
                    max_size = s.get("size", 0.0)
            block_lines.append(line_text)

        if not block_lines:
            continue

        paragraph = re.sub(r"\s+", " ", " ".join(block_lines)).strip()
        if len(paragraph) <= 3 and paragraph.isdigit():
            continue

        if max_size >= body_size + 4.0 and len(paragraph) < 140:
            lines_out.append(f"## {paragraph}")
        elif max_size >= body_size + 2.0 and len(paragraph) < 140:
            lines_out.append(f"### {paragraph}")
        else:
            lines_out.append(paragraph)

    return "\n\n".join(lines_out)


def process_single_pdf(
    pdf_path: str,
    cache_dir: str,
    force: bool = False,
    detect_tables: bool = True,
) -> dict:
    filename = os.path.basename(pdf_path)
    stem = Path(filename).stem
    cache_path = os.path.join(cache_dir, f"{stem}.json")
    file_hash = sha256_file(pdf_path)
    meta = infer_metadata(filename)
    if meta is None:
        raise ValueError(f"Archivo ignorado por filtro CONALITEG: {filename}")

    if not force and os.path.exists(cache_path):
        try:
            with open(cache_path, "r", encoding="utf-8") as handle:
                cached = json.load(handle)
            if cached.get("sha256") == file_hash and "pages" in cached:
                print(
                    f"OK [Cache] {filename} (Grado {meta['grade']}): "
                    f"{cached.get('page_count')} pags | tablas: {cached.get('pages_with_tables', 0)}",
                    flush=True,
                )
                return cached
        except Exception:
            pass

    doc = fitz.open(pdf_path)
    total_pages = doc.page_count
    pages_data = []
    full_markdown_parts = []
    pages_with_tables = 0

    # En Multiples Lenguajes (MLA) son cuentos/lecturas sin tablas curriculares
    run_table_finder = detect_tables and meta["kind"] != "MLA"

    for idx, page in enumerate(doc):
        page_num = idx + 1
        md_text = extract_structured_page_text(page)
        md_tables = extract_tables_as_markdown(page) if run_table_finder else []
        images = page.get_images(full=True)

        if md_tables:
            pages_with_tables += 1
            tables_block = "\n\n".join(md_tables)
            md_text = f"{md_text}\n\n### Tablas de la pagina {page_num}\n\n{tables_block}".strip()

        pages_data.append(
            {
                "page_number": page_num,
                "char_count": len(md_text),
                "table_count": len(md_tables),
                "image_count": len(images),
                "markdown": md_text,
            }
        )
        if md_text:
            full_markdown_parts.append(f"<!-- PAGE {page_num} -->\n{md_text}")

    doc.close()

    payload = {
        "schema_version": "2.0",
        "source": filename,
        "sha256": file_hash,
        "extracted_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        **meta,
        "page_count": total_pages,
        "pages_with_tables": pages_with_tables,
        "content": "\n\n".join(full_markdown_parts),
        "pages": pages_data,
    }

    os.makedirs(cache_dir, exist_ok=True)
    with open(cache_path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)

    print(
        f"EXTRAIDO {filename} (Grado {meta['grade']} - {meta['book_type']}): "
        f"{total_pages} pags | pags con tablas: {pages_with_tables}",
        flush=True,
    )
    return payload


def process_all_pdfs(
    data_dir: str = str(REPO_ROOT / "data"),
    cache_dir: str = str(REPO_ROOT / "data" / "cache"),
    grades: list[int] | None = None,
    force: bool = False,
    detect_tables: bool = True,
    workers: int = 4,
) -> list[dict]:
    os.makedirs(cache_dir, exist_ok=True)
    target_files = []

    for filename in sorted(os.listdir(data_dir)):
        meta = infer_metadata(filename)
        if meta is None:
            continue
        if grades is not None and meta["grade"] not in grades:
            continue
        target_files.append(os.path.join(data_dir, filename))

    results = []
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {
            pool.submit(
                process_single_pdf,
                pdf_path=path,
                cache_dir=cache_dir,
                force=force,
                detect_tables=detect_tables,
            ): path
            for path in target_files
        }
        for future in as_completed(futures):
            results.append(future.result())

    results.sort(key=lambda r: r["source"])
    return results


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--grade", type=int, nargs="+", choices=range(1, 7), help="Filtrar por grado(s) 1..6.")
    parser.add_argument("--all-books", action="store_true", help="Extraer los 30 libros de 1.o a 6.o grado.")
    parser.add_argument("--force", action="store_true", help="Re-extraer aunque ya exista en data/cache/.")
    parser.add_argument("--no-tables", action="store_true", help="Omitir detector de tablas para extraccion rapida.")
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--data-dir", default=str(REPO_ROOT / "data"))
    parser.add_argument("--cache-dir", default=str(REPO_ROOT / "data" / "cache"))
    args = parser.parse_args(argv)

    grades = None if args.all_books else (args.grade or list(range(1, 7)))
    results = process_all_pdfs(
        data_dir=args.data_dir,
        cache_dir=args.cache_dir,
        grades=grades,
        force=args.force,
        detect_tables=not args.no_tables,
        workers=args.workers,
    )

    total_pages = sum(r["page_count"] for r in results)
    total_tables = sum(r["pages_with_tables"] for r in results)
    print(
        f"\n=== RESUMEN PASO 2: {len(results)} libros CONALITEG extraidos (Programa Sintetico omitido) | "
        f"{total_pages} paginas | {total_tables} paginas con tablas Markdown ===",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())