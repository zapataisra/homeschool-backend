"""Consulta de prueba: compara latencia y resultados de vectorial vs. híbrida (usa retrieval.py)."""
import sys
import time

from dotenv import load_dotenv

try:
    from app.adapters.embedding import get_embedder
    from app.adapters.retrieval import connect, search
except ModuleNotFoundError:
    from embedding import get_embedder
    from retrieval import connect, search


def main(question: str, limit: int = 5) -> None:
    load_dotenv()
    _, embed_query = get_embedder()
    qv = embed_query(question)                      # el cómputo del embedding no se mide aquí
    with connect() as conn:
        timings, results = {}, {}
        for mode in ("vector", "keyword", "hybrid"):
            t0 = time.perf_counter()
            results[mode] = search(conn, qv, question, mode=mode, limit=limit)
            timings[mode] = (time.perf_counter() - t0) * 1000
    print(f"\nPregunta: {question}")
    print("Latencia SQL: " + " | ".join(f"{m} {ms:.1f} ms" for m, ms in timings.items()))
    for i, r in enumerate(results["hybrid"], 1):
        print(f"[{i}] id {r['id']} | {r['source']} chunk {r['chunk_id']} | {r['texto'][:110]!r}")


if __name__ == "__main__":
    main(" ".join(sys.argv[1:]) or "¿Cómo se aborda el pensamiento crítico en el aula?")
