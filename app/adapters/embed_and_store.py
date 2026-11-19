#!/usr/bin/env python3
"""PASO 4 DE 4: Vectorizacion por lotes e insercion incremental en public.embeddings_sep.

- NO ejecuta TRUNCATE (conserva intactos los vectores de Programa_Sintetico_Fase_3/4/5).
- Procesa en lotes (batch_size=64) con all-MiniLM-L6-v2 (384 dims, $0.00 USD).
- Es idempotente por libro: si un libro ya existe en embeddings_sep lo omite,
  o lo reemplaza con DELETE WHERE source = %s si se pasa --force.

Uso:
  python app/adapters/embed_and_store.py --all-books
  python app/adapters/embed_and_store.py --grade 4
"""

import argparse
import json
import os
import re
import sys
import time
from pathlib import Path

import psycopg
from dotenv import load_dotenv
from sentence_transformers import SentenceTransformer

load_dotenv()

REPO_ROOT = Path(__file__).resolve().parents[2]

DB_USER = os.getenv("POSTGRES_USER", "admin")
DB_PASS = os.getenv("POSTGRES_PASSWORD", "admin123")
DB_NAME = os.getenv("POSTGRES_DB", "vectordb")
DB_HOST = os.getenv("POSTGRES_HOST", "localhost")
DB_PORT = os.getenv("POSTGRES_PORT", "5432")

DB_URL = f"postgresql://{DB_USER}:{DB_PASS}@{DB_HOST}:{DB_PORT}/{DB_NAME}"
DEFAULT_CHUNKS_DIR = str(REPO_ROOT / "data" / "chunks")
CONALITEG_JSONL_RE = re.compile(r"^P([1-6])([A-Z]{3})_.*\.jsonl$", re.IGNORECASE)


def parse_int_chunk_id(raw_id, fallback_index: int) -> int:
    """Garantiza que chunk_id sea integer compatible con la columna chunk_id integer."""
    if isinstance(raw_id, int):
        return raw_id
    if isinstance(raw_id, str) and raw_id.isdigit():
        return int(raw_id)
    return fallback_index


def embed_and_store(
    chunks_dir: str = DEFAULT_CHUNKS_DIR,
    grades: list[int] | None = None,
    batch_size: int = 64,
    force: bool = False,
):
    print("Cargando modelo local all-MiniLM-L6-v2 (384 dimensiones)...", flush=True)
    model = SentenceTransformer("all-MiniLM-L6-v2")

    total_chunks = 0
    total_books = 0
    total_embed_time = 0.0
    total_db_time = 0.0

    files_to_process = []
    for filename in sorted(os.listdir(chunks_dir)):
        if not filename.endswith(".jsonl"):
            continue
        match = CONALITEG_JSONL_RE.match(filename)
        if not match:
            # Ignora Programa_Sintetico_*.jsonl para no tocar lo que ya esta en la BD
            continue
        grade = int(match.group(1))
        if grades is not None and grade not in grades:
            continue
        files_to_process.append((grade, filename))

    with psycopg.connect(DB_URL) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                CREATE TABLE IF NOT EXISTS embeddings_sep (
                    id serial PRIMARY KEY,
                    source text,
                    chunk_id integer,
                    texto text,
                    embedding vector(384)
                );
                """
            )
            conn.commit()

            for grade, filename in files_to_process:
                filepath = os.path.join(chunks_dir, filename)
                records = []
                with open(filepath, "r", encoding="utf-8") as f:
                    for idx, line in enumerate(f, start=1):
                        if not line.strip():
                            continue
                        data = json.loads(line)
                        records.append(
                            (
                                data["source"],
                                parse_int_chunk_id(data.get("chunk_id"), idx),
                                data["text"],
                            )
                        )

                if not records:
                    continue

                source_name = records[0][0]
                cur.execute(
                    "SELECT COUNT(*) FROM embeddings_sep WHERE source = %s;",
                    (source_name,),
                )
                existing_count = cur.fetchone()[0]

                if existing_count > 0 and not force:
                    print(
                        f"OK [Omitido] {filename}: ya tiene {existing_count} chunks en embeddings_sep.",
                        flush=True,
                    )
                    continue

                if existing_count > 0 and force:
                    cur.execute(
                        "DELETE FROM embeddings_sep WHERE source = %s;",
                        (source_name,),
                    )

                book_embed_time = 0.0
                book_db_time = 0.0

                for start_idx in range(0, len(records), batch_size):
                    batch = records[start_idx : start_idx + batch_size]
                    texts = [item[2] for item in batch]

                    t0 = time.perf_counter()
                    vectors = model.encode(
                        texts,
                        batch_size=batch_size,
                        show_progress_bar=False,
                    ).tolist()
                    book_embed_time += time.perf_counter() - t0

                    rows_to_insert = [
                        (batch[i][0], batch[i][1], batch[i][2], str(vectors[i]))
                        for i in range(len(batch))
                    ]

                    t1 = time.perf_counter()
                    cur.executemany(
                        """
                        INSERT INTO embeddings_sep (source, chunk_id, texto, embedding)
                        VALUES (%s, %s, %s, %s)
                        """,
                        rows_to_insert,
                    )
                    book_db_time += time.perf_counter() - t1

                conn.commit()
                total_books += 1
                total_chunks += len(records)
                total_embed_time += book_embed_time
                total_db_time += book_db_time

                print(
                    f"VECTORIZADO [Grado {grade}] {source_name}: {len(records)} chunks | "
                    f"embed: {book_embed_time:.2f}s | db: {book_db_time:.2f}s",
                    flush=True,
                )

            cur.execute(
                """
                SELECT
                    COUNT(*) AS total_rows,
                    COUNT(*) FILTER (WHERE source ILIKE '%Programa_Sintetico%') AS programa_sintetico_rows,
                    COUNT(*) FILTER (WHERE source NOT ILIKE '%Programa_Sintetico%') AS libros_conaliteg_rows
                FROM embeddings_sep;
                """
            )
            totals = cur.fetchone()

    print("\n=== RESUMEN PASO 4: VECTORIZACION COMPLETADA ===")
    print(f"Libros CONALITEG procesados en esta corrida: {total_books}")
    print(f"Chunks nuevos insertados: {total_chunks:,}")
    print(f"Tiempo total de embedding (all-MiniLM-L6-v2 local): {total_embed_time:.2f} s")
    print(f"Tiempo total de insercion (pgvector): {total_db_time:.2f} s")
    print(
        f"Estado actual de public.embeddings_sep -> Total: {totals[0]:,} | "
        f"Programa Sintetico (intacto): {totals[1]:,} | Libros CONALITEG: {totals[2]:,}",
        flush=True,
    )


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--grade", type=int, nargs="+", choices=range(1, 7), help="Filtrar por grado (1..6).")
    parser.add_argument("--all-books", action="store_true", help="Vectorizar los 30 libros de CONALITEG.")
    parser.add_argument("--batch-size", type=int, default=64)
    parser.add_argument("--force", action="store_true", help="Reemplazar vectores si el libro ya existia.")
    parser.add_argument("--chunks-dir", default=DEFAULT_CHUNKS_DIR)
    args = parser.parse_args(argv)

    grades = None if args.all_books else (args.grade or list(range(1, 7)))
    embed_and_store(
        chunks_dir=args.chunks_dir,
        grades=grades,
        batch_size=args.batch_size,
        force=args.force,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())