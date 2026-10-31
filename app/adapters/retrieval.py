"""
Recuperación (RAG): búsqueda vectorial, léxica e híbrida con RRF.

UNA sola implementación, usada por la aplicación y por la evaluación, para que
se mida exactamente lo que se usa en producción.

Decisiones:
- Se une SIEMPRE por `id` (clave primaria). `chunk_id` solo es único dentro de un documento.
- La parte léxica usa OR entre términos: websearch_to_tsquery/plainto_tsquery exigen TODOS los
  términos (AND), y con una pregunta larga casi nunca devuelven nada.
- Cada rama toma un conjunto candidato (`pool`) y RRF fusiona los rangos (k = 60).
"""
import os
import re
from typing import Literal

import psycopg

RRF_K = 60
Mode = Literal["vector", "keyword", "hybrid"]


def connect() -> psycopg.Connection:
    return psycopg.connect(
        host=os.getenv("POSTGRES_HOST", "localhost"),
        port=int(os.getenv("POSTGRES_PORT", "5432")),
        user=os.getenv("POSTGRES_USER"),
        password=os.getenv("POSTGRES_PASSWORD"),
        dbname=os.getenv("POSTGRES_DB"),
    )


def build_tsquery(question: str) -> str:
    """Términos de la pregunta unidos con OR. Solo caracteres \\w: no hay inyección en to_tsquery."""
    terms = list(dict.fromkeys(re.findall(r"\w{3,}", question.lower())))
    return " | ".join(terms)


def _vec(v: list[float]) -> str:
    return "[" + ",".join(f"{x:.7f}" for x in v) + "]"


SQL_VECTOR = """
SELECT id, source, chunk_id, texto, embedding <=> %(qv)s::vector AS score
FROM embeddings_sep
ORDER BY embedding <=> %(qv)s::vector
LIMIT %(limit)s
"""

SQL_KEYWORD = """
WITH q AS (SELECT to_tsquery('spanish', %(tsq)s) AS tsq)
SELECT e.id, e.source, e.chunk_id, e.texto, ts_rank_cd(e.fts, q.tsq) AS score
FROM embeddings_sep e, q
WHERE e.fts @@ q.tsq
ORDER BY score DESC
LIMIT %(limit)s
"""

SQL_HYBRID = """
WITH q AS (SELECT to_tsquery('spanish', %(tsq)s) AS tsq),
semantic AS (
    SELECT id, ROW_NUMBER() OVER (ORDER BY dist) AS rnk
    FROM (SELECT id, embedding <=> %(qv)s::vector AS dist
          FROM embeddings_sep
          ORDER BY embedding <=> %(qv)s::vector
          LIMIT %(pool)s) t
),
keyword AS (
    SELECT id, ROW_NUMBER() OVER (ORDER BY score DESC) AS rnk
    FROM (SELECT e.id, ts_rank_cd(e.fts, q.tsq) AS score
          FROM embeddings_sep e, q
          WHERE e.fts @@ q.tsq
          ORDER BY score DESC
          LIMIT %(pool)s) t
),
fused AS (
    SELECT id,
           COALESCE(1.0 / (%(k)s + s.rnk), 0) + COALESCE(1.0 / (%(k)s + k.rnk), 0) AS rrf
    FROM semantic s FULL OUTER JOIN keyword k USING (id)
)
SELECT e.id, e.source, e.chunk_id, e.texto, f.rrf::float8 AS score
FROM fused f JOIN embeddings_sep e USING (id)
ORDER BY f.rrf DESC, e.id
LIMIT %(limit)s
"""


def search(conn: psycopg.Connection, query_vector: list[float] | None, question: str,
           mode: Mode = "hybrid", limit: int = 10, pool: int = 20) -> list[dict]:
    params = {"limit": limit, "pool": max(pool, limit), "k": RRF_K,
              "tsq": build_tsquery(question),
              "qv": _vec(query_vector) if query_vector is not None else None}
    if mode in ("vector", "hybrid") and query_vector is None:
        raise ValueError("Este modo necesita el vector de la consulta")
    if mode in ("keyword", "hybrid") and not params["tsq"]:
        if mode == "keyword":
            return []
        mode = "vector"                       # sin términos útiles: solo vectorial
    sql = {"vector": SQL_VECTOR, "keyword": SQL_KEYWORD, "hybrid": SQL_HYBRID}[mode]
    with conn.cursor() as cur:
        cur.execute(sql, params)
        cols = [d.name for d in cur.description]
        return [dict(zip(cols, row)) for row in cur.fetchall()]
