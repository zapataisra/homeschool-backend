#!/usr/bin/env python3
"""PASO 3 DE 4: Chunking estructural con ruta de titulos y proteccion de tablas Markdown.

Lee los 30 libros de CONALITEG desde data/cache/*.json (omitiendo Programa_Sintetico_*)
y escribe data/chunks/{stem}.jsonl conservando compatibilidad con {chunk_id, source, text}
mas metadatos curriculares (grade, fase, book_code, page_number, heading_path, has_table).

Uso:
  python app/adapters/chunking.py --all-books
  python app/adapters/chunking.py --grade 4
"""

import argparse
import json
import os
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CONALITEG_STEM_RE = re.compile(r"^P([1-6])([A-Z]{3})_", re.IGNORECASE)

BOILERPLATE_PATTERNS = (
    "secretaría de educación pública",
    "comisión nacional de libros de texto gratuitos",
    "impreso en méxico",
    "distribución gratuita-prohibida su venta",
    "créditos iconográficos",
)


def create_chunks(text: str, chunk_size: int = 1000, overlap: int = 200) -> list[str]:
    """Funcion legacy conservada por compatibilidad con modulos existentes."""
    if not text:
        return []
    chunks = []
    start = 0
    text_len = len(text)
    while start < text_len:
        end = min(start + chunk_size, text_len)
        chunk = text[start:end].strip()
        if chunk:
            chunks.append(chunk)
        if end >= text_len:
            break
        start = max(end - overlap, start + 1)
    return chunks


def is_boilerplate_page(page_num: int, total_pages: int, md_text: str) -> bool:
    """Descarta portadas, creditos legales vacios o colofon sin contenido pedagogico."""
    clean = md_text.strip()
    if len(clean) < 40:
        return True
    if page_num <= 5 or page_num >= total_pages - 2:
        low = clean.lower()
        hits = sum(1 for pat in BOILERPLATE_PATTERNS if pat in low)
        if hits >= 2 and len(clean) < 900:
            return True
    return False


def split_markdown_table(table_block: str, max_chars: int) -> list[str]:
    """Si una tabla Markdown supera max_chars, la divide por filas repitiendo el encabezado."""
    lines = [ln for ln in table_block.strip().splitlines() if ln.strip()]
    if len(lines) <= 2 or len(table_block) <= max_chars:
        return [table_block.strip()]

    header_block = "\n".join(lines[:2])
    chunks = []
    current_rows = []
    current_len = len(header_block)

    for row in lines[2:]:
        row_len = len(row) + 1
        if current_rows and (current_len + row_len > max_chars):
            chunks.append(header_block + "\n" + "\n".join(current_rows))
            current_rows = [row]
            current_len = len(header_block) + row_len
        else:
            current_rows.append(row)
            current_len += row_len

    if current_rows:
        chunks.append(header_block + "\n" + "\n".join(current_rows))

    return chunks


def chunk_structured_page(
    page_md: str,
    context_prefix: str,
    current_heading: str,
    chunk_size: int = 1100,
    overlap: int = 180,
) -> tuple[list[tuple[str, str, bool]], str]:
    """Divide una pagina respetando encabezados y manteniendo tablas Markdown atomicas.

    Devuelve: (lista de (chunk_text_con_prefijo, heading_usado, has_table), ultimo_heading)
    """
    blocks = [b.strip() for b in re.split(r"\n\s*\n", page_md) if b.strip()]
    results = []
    buffer_blocks = []
    buffer_len = 0
    buffer_has_table = False
    active_heading = current_heading

    def flush_buffer():
        nonlocal buffer_blocks, buffer_len, buffer_has_table
        if not buffer_blocks:
            return
        body = "\n\n".join(buffer_blocks).strip()
        if len(body) >= 35:
            header_line = (
                f"{context_prefix} | Tema: {active_heading}]"
                if active_heading
                else f"{context_prefix}]"
            )
            full_text = f"{header_line}\n{body}"
            results.append((full_text, active_heading, buffer_has_table))
        # Conserva solape suave solo si no es tabla
        if overlap > 0 and len(buffer_blocks) > 1 and not buffer_has_table:
            tail = buffer_blocks[-1]
            if len(tail) <= overlap:
                buffer_blocks = [tail]
                buffer_len = len(tail)
                buffer_has_table = False
                return
        buffer_blocks = []
        buffer_len = 0
        buffer_has_table = False

    for block in blocks:
        if block.startswith("## ") or block.startswith("### "):
            heading_candidate = re.sub(r"^#+\s*", "", block).strip()
            if not heading_candidate.lower().startswith("tablas de la pagina"):
                if buffer_len >= 350:
                    flush_buffer()
                active_heading = heading_candidate[:120]
            buffer_blocks.append(block)
            buffer_len += len(block) + 2
            continue

        is_table = block.startswith("|") and "\n| ---" in block
        if is_table:
            table_parts = split_markdown_table(block, max_chars=chunk_size)
            for t_part in table_parts:
                if buffer_len + len(t_part) > chunk_size and buffer_blocks:
                    flush_buffer()
                buffer_blocks.append(t_part)
                buffer_len += len(t_part) + 2
                buffer_has_table = True
                if buffer_len >= chunk_size * 0.85:
                    flush_buffer()
            continue

        if buffer_len + len(block) > chunk_size and buffer_blocks:
            flush_buffer()

        if len(block) > chunk_size:
            sub_parts = create_chunks(block, chunk_size=chunk_size, overlap=overlap)
            for sp in sub_parts:
                buffer_blocks.append(sp)
                buffer_len += len(sp)
                flush_buffer()
        else:
            buffer_blocks.append(block)
            buffer_len += len(block) + 2

    flush_buffer()
    return results, active_heading


def process_single_cache(
    cache_path: str,
    chunks_dir: str,
    chunk_size: int = 1100,
    overlap: int = 180,
) -> dict:
    with open(cache_path, "r", encoding="utf-8") as handle:
        data = json.load(handle)

    source = data.get("source", os.path.basename(cache_path).replace(".json", ".pdf"))
    stem = Path(source).stem
    book_code = data.get("book_code", stem.split("_")[0])
    grade = data.get("grade")
    fase = data.get("fase")
    book_type = data.get("book_type", "Libro SEP")
    pages = data.get("pages", [])
    total_pages = data.get("page_count", len(pages))

    os.makedirs(chunks_dir, exist_ok=True)
    out_path = os.path.join(chunks_dir, f"{stem}.jsonl")

    current_heading = ""
    chunk_records = []
    table_chunks = 0
    total_chars = 0

    for page in pages:
        page_num = page.get("page_number", 0)
        md_text = page.get("markdown", "")
        if is_boilerplate_page(page_num, total_pages, md_text):
            continue

        context_prefix = (
            f"[Grado {grade}° Primaria | {fase} | {book_type} ({book_code}) | Pág. {page_num}"
        )
        page_chunks, current_heading = chunk_structured_page(
            page_md=md_text,
            context_prefix=context_prefix,
            current_heading=current_heading,
            chunk_size=chunk_size,
            overlap=overlap,
        )

        for local_idx, (chunk_text, heading_used, has_table) in enumerate(page_chunks, start=1):
            chunk_id = f"{book_code}_p{page_num:03d}_c{local_idx:02d}"
            char_len = len(chunk_text)
            total_chars += char_len
            if has_table:
                table_chunks += 1

            chunk_records.append(
                {
                    "schema_version": "2.0",
                    "chunk_id": chunk_id,
                    "source": source,
                    "book_code": book_code,
                    "grade": grade,
                    "fase": fase,
                    "book_type": book_type,
                    "page_number": page_num,
                    "heading_path": heading_used,
                    "has_table": has_table,
                    "char_count": char_len,
                    "text": chunk_text,
                }
            )

    with open(out_path, "w", encoding="utf-8") as out_f:
        for rec in chunk_records:
            out_f.write(json.dumps(rec, ensure_ascii=False) + "\n")

    est_tokens = total_chars // 4
    print(
        f"CHUNKS {stem} (Grado {grade}): {len(chunk_records)} chunks | "
        f"con tablas: {table_chunks} | ~{est_tokens:,} tokens",
        flush=True,
    )
    return {
        "stem": stem,
        "grade": grade,
        "chunk_count": len(chunk_records),
        "table_chunks": table_chunks,
        "est_tokens": est_tokens,
    }


def process_all_caches(
    cache_dir: str = str(REPO_ROOT / "data" / "cache"),
    chunks_dir: str = str(REPO_ROOT / "data" / "chunks"),
    grades: list[int] | None = None,
    chunk_size: int = 1100,
    overlap: int = 180,
) -> list[dict]:
    summaries = []
    for fname in sorted(os.listdir(cache_dir)):
        if not fname.endswith(".json"):
            continue
        match = CONALITEG_STEM_RE.match(fname)
        if not match:
            continue
        grade = int(match.group(1))
        if grades is not None and grade not in grades:
            continue
        c_path = os.path.join(cache_dir, fname)
        summaries.append(
            process_single_cache(
                cache_path=c_path,
                chunks_dir=chunks_dir,
                chunk_size=chunk_size,
                overlap=overlap,
            )
        )
    return summaries


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--grade", type=int, nargs="+", choices=range(1, 7), help="Filtrar por grado (1..6).")
    parser.add_argument("--all-books", action="store_true", help="Procesar los 30 libros de CONALITEG.")
    parser.add_argument("--chunk-size", type=int, default=1100)
    parser.add_argument("--overlap", type=int, default=180)
    parser.add_argument("--cache-dir", default=str(REPO_ROOT / "data" / "cache"))
    parser.add_argument("--chunks-dir", default=str(REPO_ROOT / "data" / "chunks"))
    args = parser.parse_args(argv)

    grades = None if args.all_books else (args.grade or list(range(1, 7)))
    summaries = process_all_caches(
        cache_dir=args.cache_dir,
        chunks_dir=args.chunks_dir,
        grades=grades,
        chunk_size=args.chunk_size,
        overlap=args.overlap,
    )

    total_chunks = sum(s["chunk_count"] for s in summaries)
    total_table_chunks = sum(s["table_chunks"] for s in summaries)
    total_tokens = sum(s["est_tokens"] for s in summaries)
    openai_cost_usd = (total_tokens / 1_000_000) * 0.02

    print(
        f"\n=== RESUMEN PASO 3: {len(summaries)} libros chunkeados | "
        f"{total_chunks:,} chunks totales | {total_table_chunks:,} chunks con tablas | "
        f"~{total_tokens:,} tokens | Costo estimado OpenAI (text-embedding-3-small): ${openai_cost_usd:.4f} USD ===",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())