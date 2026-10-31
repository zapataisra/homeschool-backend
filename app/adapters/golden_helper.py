"""
Ayuda para reetiquetar tu golden set viejo (el que usa chunk_id_esperado).

Para cada pregunta muestra los chunks que tenían esos números, en TODOS los documentos,
con el inicio de su texto. Tú eliges el que realmente responde la pregunta y copias una frase
corta y literal (4 a 10 palabras) al campo "evidencia" del golden set nuevo.

Uso (desde la raíz):  python app/adapters/golden_helper.py
Opcional:             python app/adapters/golden_helper.py ruta/al/golden_set_viejo.json
"""
import json
import sys

from dotenv import load_dotenv

try:
    from app.adapters.retrieval import connect
except ModuleNotFoundError:
    from retrieval import connect


def main(path: str = "data/evaluation/golden_set.json") -> None:
    load_dotenv()
    with open(path, encoding="utf-8") as f:
        golden = json.load(f)
    with connect() as conn, conn.cursor() as cur:
        for n, item in enumerate(golden, 1):
            ids = item.get("chunk_id_esperado", [])
            print("=" * 100)
            print(f"Pregunta {n}: {item['pregunta']}")
            print(f"(numeros de chunk viejos: {ids})")
            cur.execute(
                "SELECT id, source, chunk_id, texto FROM embeddings_sep "
                "WHERE chunk_id = ANY(%s) ORDER BY chunk_id, source", (ids,))
            for id_, source, chunk_id, texto in cur.fetchall():
                print(f"\n  [id {id_}] {source} | chunk {chunk_id}")
                print("  " + " ".join(texto.split())[:350])


if __name__ == "__main__":
    main(*sys.argv[1:2])
