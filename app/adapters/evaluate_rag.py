"""
Evaluación del retrieval: compara búsqueda vectorial, léxica e híbrida con Hit@k y MRR.

Uso (desde la raíz del proyecto):
    python app/adapters/evaluate_rag.py
    python app/adapters/evaluate_rag.py data/evaluation/golden_set.json

Formato del golden set (por pregunta):
    {"pregunta": "...",
     "evidencia": ["frase literal corta del PDF", "otra frase equivalente"],
     "source": "Programa_Sintetico_Fase_4.pdf"}          # opcional

Un resultado es relevante si su texto CONTIENE alguna frase de evidencia (sin tildes ni
mayúsculas, con saltos de línea normalizados). Así el golden set no depende de la numeración
de los chunks y sigue valiendo si cambias el tamaño, el solapamiento o la estrategia de chunking.
(Alternativa: "ids_esperados": [ids de la tabla embeddings_sep], válidos solo para esa ingesta.)

Variables: EMBED_MODEL (por defecto all-MiniLM-L6-v2) y las de conexión POSTGRES_*.
"""
import json
import os
import re
import sys
import time
import unicodedata
from datetime import datetime
from pathlib import Path

from dotenv import load_dotenv

try:
    from app.adapters.embedding import get_embedder
    from app.adapters.retrieval import connect, search
except ModuleNotFoundError:
    from embedding import get_embedder
    from retrieval import connect, search

MODES = ["vector", "keyword", "hybrid"]
KS = (1, 3, 5)


def norm(text: str) -> str:
    text = re.sub(r"-\s*\n\s*", "", text)                      # palabras cortadas al final de línea
    text = unicodedata.normalize("NFKD", text.lower())
    text = "".join(c for c in text if not unicodedata.combining(c))
    return re.sub(r"\s+", " ", text).strip()


def is_relevant(row: dict, item: dict) -> bool:
    if item.get("ids_esperados"):
        return row["id"] in item["ids_esperados"]
    if item.get("source") and row["source"] != item["source"]:
        return False
    body = norm(row["texto"])
    return any(norm(e) in body for e in item.get("evidencia", []))


def evaluate(golden: list[dict], embed_query, conn, modes=MODES, limit: int = 10) -> dict:
    vectors = {i: embed_query(item["pregunta"]) for i, item in enumerate(golden)}
    out = {}
    for mode in modes:
        hits = {k: 0 for k in KS}
        rr, latencies, misses, empty = 0.0, [], [], 0
        for i, item in enumerate(golden):
            t0 = time.perf_counter()
            rows = search(conn, vectors[i], item["pregunta"], mode=mode, limit=limit)
            latencies.append((time.perf_counter() - t0) * 1000)
            empty += not rows
            rel = [is_relevant(r, item) for r in rows]
            for k in KS:
                hits[k] += any(rel[:k])
            first = next((n for n, ok in enumerate(rel, 1) if ok), None)
            if first:
                rr += 1 / first
            else:
                misses.append(item["pregunta"])
        n = len(golden)
        out[mode] = {
            **{f"hit@{k}": round(hits[k] / n, 4) for k in KS},
            "mrr@10": round(rr / n, 4),
            "latencia_ms_media": round(sum(latencies) / n, 2),
            "consultas_sin_resultados": empty,
            "preguntas_sin_acierto": misses,
        }
    return out


def check_model_matches(conn, model_name: str) -> None:
    with conn.cursor() as cur:
        cur.execute("SELECT DISTINCT embedding_model FROM embeddings_sep")
        stored = {r[0] for r in cur.fetchall()} - {None}
    if stored and stored != {model_name}:
        sys.exit(f"Los vectores de la tabla son de {stored} pero consultas con {model_name}. "
                 "Reindexa con el mismo modelo o cambia EMBED_MODEL.")


def main() -> None:
    load_dotenv()
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "data/evaluation/golden_set.json")
    golden = json.loads(path.read_text(encoding="utf-8"))
    if golden and "evidencia" not in golden[0] and "ids_esperados" not in golden[0]:
        sys.exit("El golden set usa chunk_id_esperado, que es ambiguo entre documentos. "
                 "Reetiqueta con 'evidencia' (frases literales del PDF); ver el encabezado de este archivo.")

    model_name, embed_query = get_embedder()
    with connect() as conn:
        check_model_matches(conn, model_name)
        results = evaluate(golden, embed_query, conn)

    print(f"\nEvaluación sobre {len(golden)} preguntas | modelo: {model_name}\n")
    print(f"{'modo':<9}" + "".join(f"{'hit@' + str(k):>8}" for k in KS) + f"{'mrr@10':>9}{'ms':>8}{'vacías':>8}")
    for mode, r in results.items():
        print(f"{mode:<9}" + "".join(f"{r[f'hit@{k}']:>8.2f}" for k in KS)
              + f"{r['mrr@10']:>9.3f}{r['latencia_ms_media']:>8.1f}{r['consultas_sin_resultados']:>8}")
    for q in results["hybrid"]["preguntas_sin_acierto"]:
        print(f"  sin acierto (híbrida): {q[:90]}")

    runs = Path("data/evaluation/runs")
    runs.mkdir(parents=True, exist_ok=True)
    out = runs / f"{datetime.now():%Y%m%d_%H%M%S}.json"
    out.write_text(json.dumps({"modelo": model_name, "preguntas": len(golden), "resultados": results},
                              ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nGuardado en {out}")


if __name__ == "__main__":
    main()
